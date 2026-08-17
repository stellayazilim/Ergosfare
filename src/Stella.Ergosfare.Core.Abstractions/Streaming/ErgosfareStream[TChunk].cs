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
public abstract class ErgosfareStream<TChunk> : IMessage, IAsyncEnumerable<TChunk>
{
    /// <summary>
    /// How many chunks a bounded channel holds before a writer has to wait.
    /// </summary>
    /// <remarks>
    /// Small on purpose. The buffer exists to keep the handler fed across a scheduling gap,
    /// not to hold the payload — a larger window buys throughput only when the producer is
    /// bursty, and costs memory proportional to the chunk size.
    /// </remarks>
    public const int DefaultCapacity = 4;

    private readonly Channel<TChunk> _channel;
    private readonly IAsyncEnumerable<TChunk>? _adopted;
    private readonly Stopwatch _elapsed = new();

    private int _readerTaken;
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
    public StreamInfo Info => new(Interlocked.Read(ref _chunks), _elapsed.Elapsed, _completion);

    /// <summary>
    /// Whether this stream was created over an existing source rather than to be written to.
    /// </summary>
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
    public ValueTask WriteAsync(TChunk chunk, CancellationToken cancellationToken = default)
    {
        ThrowIfAdopted();

        return _channel.Writer.WriteAsync(chunk, cancellationToken);
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
    public bool TryWrite(TChunk chunk)
    {
        ThrowIfAdopted();

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
    public void Complete()
    {
        ThrowIfAdopted();

        if (_channel.Writer.TryComplete())
        {
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
    public void Fault(Exception exception)
    {
        if (_channel.Writer.TryComplete(exception))
        {
            Settle(StreamCompletion.Faulted);
        }
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
    public IAsyncEnumerator<TChunk> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _readerTaken, 1) == 1)
        {
            throw new InvalidOperationException(
                $"The chunks of '{GetType()}' were already taken. A stream message is single-pass: whoever reads it " +
                "consumes it, so it cannot be read twice or read by a stage before the handler.");
        }

        return Read(cancellationToken).GetAsyncEnumerator(cancellationToken);
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
        }
    }

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
        if (_adopted is not null)
        {
            throw new InvalidOperationException(
                $"'{GetType()}' was created over a source that already exists, so it has no writing side. Write to " +
                "the underlying source, or create the stream with the capacity constructor instead.");
        }
    }
}
