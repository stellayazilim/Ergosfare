using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Threading.Channels;

namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>
/// A message whose payload arrives in chunks instead of all at once.
/// </summary>
/// <typeparam name="TChunk">The chunk type the message carries.</typeparam>
/// <remarks>
/// <para>
/// The message is the stream: what makes a dispatch streaming is the message's own type,
/// not the verb it is sent with and not the module it belongs to. A command and a query
/// stream the same way, and nothing on the dispatch surface changes — a handler takes this
/// message like any other and pulls the chunks out of it.
/// </para>
/// <para>
/// The channel is bounded, so a producer faster than the handler waits rather than filling
/// memory: back-pressure is the point, and a four-gigabyte upload never exists as one value.
/// It is also single-pass. Nothing here can be replayed, which is why a stream message has
/// no retry and no resume: the handler runs once for the whole sequence, so when it stops
/// there is nobody left to produce the rest.
/// </para>
/// <para>
/// Whoever creates the stream owns its writing end and completes it. A stream nobody
/// consumes — no handler matched, a stage refused — is faulted by the dispatch rather than
/// left open, so the next write fails instead of blocking forever.
/// </para>
/// </remarks>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public abstract class ErgosfareStream<TChunk> : ErgosfareStream, IAsyncEnumerable<TChunk>
{
    /// <summary>
    /// How many chunks a bounded channel holds before a writer has to wait.
    /// </summary>
    /// <remarks>
    /// Small on purpose. The buffer exists to keep the handler fed across a scheduling gap,
    /// not to hold the payload — a larger window buys throughput only when the producer is
    /// bursty, and costs memory proportional to the chunk size.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public const int DefaultCapacity = 4;

    private readonly Channel<TChunk> _channel;
    private readonly IAsyncEnumerable<TChunk>? _adopted;
    private readonly Stopwatch _elapsed = new();

    private int _readerTaken;
    private int _writerMode;
    private Exception? _terminalError;
    private bool _producerCompleted;
    private CancellationTokenSource? _readerCancellation;
    private long _chunks;
    private StreamCompletion _completion = StreamCompletion.Open;

    /// <summary>
    /// Creates a stream the caller writes into.
    /// </summary>
    /// <param name="capacity">
    /// How many chunks may be buffered before a write waits; defaults to
    /// <see cref="DefaultCapacity"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    protected ErgosfareStream(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                "A stream's capacity is what makes a writer wait instead of filling memory, so it must be positive.");
        }

        _channel = Channel.CreateBounded<TChunk>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Creates a stream over a source that already exists.
    /// </summary>
    /// <param name="source">The sequence to hand to the handler.</param>
    /// <remarks>
    /// The adopting form: a request body, a file, the output of an earlier dispatch. Nobody
    /// pumps it, so the caller can await the dispatch directly — the deadlock the writing
    /// form has to avoid cannot arise here.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    protected ErgosfareStream(IAsyncEnumerable<TChunk> source)
    {
        _adopted = source ?? throw new ArgumentNullException(nameof(source));
        _channel = Channel.CreateBounded<TChunk>(1);
    }

    /// <summary>
    /// What this stream has carried so far, and how it ended if it has.
    /// </summary>
    /// <remarks>
    /// One value rather than three loose members, because this is what the stages are handed:
    /// they see what the stream did, never what it carried.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public StreamInfo Info => new(Interlocked.Read(ref _chunks), _elapsed.Elapsed, _completion);

    /// <summary>
    /// Whether this stream was created over an existing source rather than to be written to.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public bool IsAdopted => _adopted is not null;

    /// <summary>
    /// Writes a chunk, waiting while the buffer is full.
    /// </summary>
    /// <param name="chunk">The chunk to write.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <remarks>
    /// Waiting is the back-pressure: it suspends nothing when the buffer has room, and when
    /// it does not, the producer slowing down is the correct behaviour.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The stream adopted an existing source.</exception>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public ValueTask WriteAsync(TChunk chunk, CancellationToken cancellationToken = default)
    {
        ThrowIfAdopted();
        return WriteFromSourceAsync(chunk, cancellationToken);
    }

    /// <summary>Writes from the single bound source without claiming the manual writer.</summary>
    protected ValueTask WriteFromSourceAsync(TChunk chunk, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // A closed channel answers with ChannelClosedException, whose message says nothing
        // about what actually happened. Where the dispatch is what closed it, say so.
        if (Volatile.Read(ref _terminalError) is { } error)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        var write = _channel.Writer.WriteAsync(chunk, cancellationToken);
        return write.IsCompletedSuccessfully ? write : ObserveWrite(write);
    }

    private static async ValueTask ObserveWrite(ValueTask write)
    {
        try { await write.ConfigureAwait(false); }
        catch (ChannelClosedException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// Writes a chunk if the buffer has room, and reports rather than waits when it does not.
    /// </summary>
    /// <param name="chunk">The chunk to write.</param>
    /// <returns><c>true</c> when the chunk was taken.</returns>
    /// <remarks>
    /// For producers that cannot wait — live capture, telemetry — where dropping is better
    /// than stalling. The decision belongs to the caller, which is why this returns instead
    /// of dropping silently.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The stream adopted an existing source.</exception>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public bool TryWrite(TChunk chunk)
    {
        ThrowIfAdopted();

        if (Volatile.Read(ref _terminalError) is { } error)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        return _channel.Writer.TryWrite(chunk);
    }

    /// <summary>
    /// Says no more chunks are coming.
    /// </summary>
    /// <remarks>
    /// The handler's enumeration ends here. Without it the handler waits for a chunk that
    /// never arrives, which is the one hazard of the writing form.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The stream adopted an existing source.</exception>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void Complete()
    {
        ThrowIfAdopted();
        CompleteFromSource();
    }

    /// <summary>Completes the writing end owned by the bound source.</summary>
    protected void CompleteFromSource()
    {
        if (_channel.Writer.TryComplete())
        {
            Volatile.Write(ref _producerCompleted, true);
            Settle(StreamCompletion.Completed);
        }
    }

    /// <summary>
    /// Ends the stream with a failure, which surfaces at the handler's next read.
    /// </summary>
    /// <param name="exception">The failure to end it with.</param>
    /// <remarks>
    /// Used by the producer when its own source broke, and by the dispatch when nothing will
    /// consume the stream. Either way the sequence is over: a faulted stream is not resumed.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        EndDispatch(exception);
    }

    /// <summary>
    /// Reads the chunks. The handler's side of the message, and it may be taken once.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>An enumerator over the chunks, in the order they were written.</returns>
    /// <remarks>
    /// The message is the sequence, so a handler writes <c>await foreach (var chunk in
    /// command)</c> and nothing stands between it and the payload. Single-pass is the
    /// contract, not an implementation detail: a network-backed sequence cannot be
    /// enumerated twice, and a second reader would silently take chunks the first one needs.
    /// The guard is here rather than in the iterator so that it fires when the enumerator is
    /// asked for, not at the first move.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The chunks were already taken.</exception>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public IAsyncEnumerator<TChunk> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (Interlocked.Exchange(ref _readerTaken, 1) == 1)
        {
            throw new InvalidOperationException(
                $"The chunks of '{GetType()}' were already taken. A stream message is single-pass: whoever reads it " +
                "consumes it, so it cannot be read twice or read by a stage before the handler.");
        }

        _readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return Read(_readerCancellation.Token).GetAsyncEnumerator();
    }

    /// <summary>
    /// Walks the chunks and keeps the counters the stages read.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The chunks.</returns>
    private async IAsyncEnumerable<TChunk> Read(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _elapsed.Start();

        try
        {
            if (_adopted is not null)
            {
                await foreach (var chunk in _adopted.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    Interlocked.Increment(ref _chunks);
                    yield return chunk;
                }
            }
            else
            {
                await foreach (var chunk in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    Interlocked.Increment(ref _chunks);
                    yield return chunk;
                }
            }
        }
        finally
        {
            _elapsed.Stop();

            if (_completion == StreamCompletion.Open)
            {
                Settle(cancellationToken.IsCancellationRequested
                    ? StreamCompletion.Cancelled
                    : StreamCompletion.Completed);
            }
            Interlocked.Exchange(ref _readerCancellation, null)?.Dispose();
        }
    }

    internal override async ValueTask DisposeCoreAsync()
    {
        if (_completion == StreamCompletion.Open)
            EndDispatch(new Stella.Ergosfare.Core.Abstractions.Exceptions.StreamOutputDisposedException());
        else
            EndDispatch();
        try { _readerCancellation?.Cancel(); }
        catch (AggregateException) { }
        catch (ObjectDisposedException) { }
        await WaitForProducerAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    internal override void EndDispatch(Exception? exception = null)
    {
        // An adopted source has no writing side, but its outcome still follows dispatch.
        if (exception is not null)
            _completion = exception is OperationCanceledException
                ? StreamCompletion.Cancelled : StreamCompletion.Faulted;
        if (_adopted is not null)
        {
            return;
        }

        if (exception is null && Volatile.Read(ref _producerCompleted))
            return;
        var error = exception ?? DispatchEnded();
        // Reader disposal may already have settled Info; it does not close the writer.
        if (_channel.Writer.TryComplete(error))
            Interlocked.CompareExchange(ref _terminalError, error, null);
        if (_completion == StreamCompletion.Open)
            Settle(StreamCompletion.Faulted);
    }

    /// <summary>
    /// The failure a stream ends with when its dispatch stopped before the payload was
    /// written.
    /// </summary>
    /// <returns>The exception.</returns>
    /// <remarks>
    /// Built fresh each time so it carries the stack of the write that hit it, rather than
    /// the stack of the dispatch that ended a while ago somewhere else.
    /// </remarks>
    private InvalidOperationException DispatchEnded()
        => new($"The dispatch of '{GetType()}' ended before its payload was written. Await the dispatch to see why " +
               "— a stage refused it, the handler failed, or it was cancelled. Writing to the stream after that " +
               "point would wait for a reader that is not coming.");

    /// <summary>
    /// Records how the stream ended, keeping the first answer.
    /// </summary>
    /// <param name="completion">How it ended.</param>
    private void Settle(StreamCompletion completion)
    {
        if (_completion == StreamCompletion.Open)
        {
            _completion = completion;
        }
    }

    /// <summary>
    /// Refuses the writing side of an adopted stream.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stream adopted an existing source.</exception>
    private void ThrowIfAdopted()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_adopted is not null || Interlocked.CompareExchange(ref _writerMode, 1, 0) == 2)
        {
            throw new InvalidOperationException(
                $"'{GetType()}' was created over a source that already exists, so it has no writing side. Write to " +
                "the underlying source, or create the stream with the capacity constructor instead.");
        }
    }

    /// <summary>Claims the writing end for one source, before any manual writes.</summary>
    protected void ClaimSource()
    {
        if (_adopted is not null || Interlocked.CompareExchange(ref _writerMode, 2, 0) != 0)
            throw new InvalidOperationException("Bind one source before writing; Pipe cannot be combined with manual writes.");
    }
}
