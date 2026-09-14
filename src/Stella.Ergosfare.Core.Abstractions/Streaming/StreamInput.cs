#pragma warning disable ERGOEXP003
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions.Exceptions;

namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>A bounded stream message whose public properties carry its metadata.</summary>
/// <typeparam name="TChunk">One buffered item; capacity is an item count, not a byte budget.</typeparam>
/// <typeparam name="TSelf">The concrete message type returned by fluent operations.</typeparam>
/// <remarks>Pipe starts producing immediately. It waits when the channel is full; dispatch
/// may start consuming later. Dispose the input if no dispatch will consume it.
/// IBufferWriter provides manual staging; call FlushAsync to publish staged items.
/// Staging grows to honor sizeHint independently of the bounded queue. Capacity does not
/// limit staging memory or the memory reachable through reference-type items.
/// Manual writing and Pipe are mutually exclusive. Concurrent buffer-writing operations
/// are not supported; IAsyncEnumerable permits exactly one consumer.</remarks>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public abstract class StreamInput<TChunk, TSelf> : ErgosfareStream<TChunk>, IBufferWriter<TChunk>
    where TSelf : StreamInput<TChunk, TSelf>
{
    private readonly int _capacity;
    private CancellationTokenSource? _producerCancellation;
    private Task? _producer;
    private TChunk[]? _staging;
    private int _staged;
    private bool _flushing;
    private bool _bound;
    private int _stopped;

    /// <summary>Creates a message with room for the given number of queued items.</summary>
    /// <param name="capacity">Maximum queued items; growable staging and an in-flight item are additional.</param>
    protected StreamInput(int capacity = 24) : base(capacity)
    {
        if (this is not TSelf)
            throw new InvalidOperationException("The self type must be the concrete stream message type.");
        _capacity = capacity;
    }

    /// <summary>Starts copying items into this message and returns the same concrete message.</summary>
    /// <param name="source">The single source to consume.</param>
    /// <param name="cancellationToken">Cancels production even before dispatch starts.</param>
    /// <returns>This message.</returns>
    public TSelf Pipe(IAsyncEnumerable<TChunk> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_staging is not null)
            throw new InvalidOperationException("Pipe cannot be combined with manual buffer writing.");
        ClaimSource();
        _bound = true;
        _producerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _producer = PumpAsync(source, _producerCancellation.Token);
        return (TSelf)this;
    }

    /// <summary>Starts copying items through a synchronous per-item transform.</summary>
    /// <param name="source">The source.</param>
    /// <param name="transform">The transformation applied once per item.</param>
    /// <param name="cancellationToken">Cancels production.</param>
    /// <returns>This message.</returns>
    public TSelf Pipe(IAsyncEnumerable<TChunk> source, Func<TChunk, TChunk> transform,
        CancellationToken cancellationToken = default) => TransformPipe(source, transform, cancellationToken);

    /// <summary>Produces one item per source item, without passing its value to the factory.</summary>
    public TSelf Pipe(IAsyncEnumerable<TChunk> source, Func<TChunk> converter,
        CancellationToken cancellationToken = default) => TransformPipe(source, converter, cancellationToken);

    /// <summary>Converts each source item using a reusable converter.</summary>
    public TSelf Pipe(IAsyncEnumerable<TChunk> source, IPipeConverter<TChunk, TChunk> converter,
        CancellationToken cancellationToken = default) => TransformPipe(source, converter, cancellationToken);

    /// <summary>Produces one output per source item using a parameterless factory.</summary>
    public TSelf TransformPipe<TSource>(IAsyncEnumerable<TSource> source, Func<TChunk> converter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return TransformPipe(source, _ => converter(), cancellationToken);
    }

    /// <summary>Converts source items using a reusable converter.</summary>
    public TSelf TransformPipe<TSource>(IAsyncEnumerable<TSource> source, IPipeConverter<TSource, TChunk> converter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return TransformPipe(source, converter.Convert, cancellationToken);
    }

    /// <summary>Converts each byte independently. Use IPipeConverter for stateful text decoding.</summary>
    public TSelf Pipe(Stream source, Func<byte, TChunk> converter, CancellationToken cancellationToken = default)
        => TransformPipe(PipeConverters.Bytes.ConvertAsync(source), converter, cancellationToken);

    /// <summary>Produces one item for each byte read, without passing the byte to the factory.</summary>
    public TSelf Pipe(Stream source, Func<TChunk> converter, CancellationToken cancellationToken = default)
        => TransformPipe(PipeConverters.Bytes.ConvertAsync(source), converter, cancellationToken);

    /// <summary>Converts individual bytes using a reusable chunk converter.</summary>
    public TSelf Pipe(Stream source, IPipeConverter<byte, TChunk> converter, CancellationToken cancellationToken = default)
        => TransformPipe(PipeConverters.Bytes.ConvertAsync(source), converter, cancellationToken);

    /// <summary>Starts converting source items into this message's chunk type.</summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transform">The item conversion.</param>
    /// <param name="cancellationToken">Cancels production.</param>
    /// <returns>This message.</returns>
    public TSelf TransformPipe<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, TChunk> transform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);
        return Pipe(Convert(source, transform), cancellationToken);
    }

    /// <summary>Starts a caller-provided converter over a byte stream; leaves the source open.</summary>
    /// <param name="source">The caller-owned byte source.</param>
    /// <param name="converter">The format and item-boundary converter.</param>
    /// <param name="cancellationToken">Cancels production.</param>
    /// <returns>This message.</returns>
    public TSelf Pipe(Stream source, IPipeConverter<TChunk> converter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(converter);
        return Pipe(Convert(source, converter), cancellationToken);
    }

    private async Task PumpAsync(IAsyncEnumerable<TChunk> source, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chunk in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                await WriteFromSourceAsync(chunk, cancellationToken).ConfigureAwait(false);
            CompleteFromSource();
        }
        catch (Exception error)
        {
            // Fault closes the queue. The task itself is observed by dispatch/disposal and
            // never leaves an unobserved exception if dispatch has not started yet.
            if (Volatile.Read(ref _stopped) == 0)
                base.EndDispatch(error);
        }
    }

    private static async IAsyncEnumerable<TChunk> Convert<TSource>(IAsyncEnumerable<TSource> source,
        Func<TSource, TChunk> transform, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return transform(chunk);
    }

    private static async IAsyncEnumerable<TChunk> Convert(Stream source, IPipeConverter<TChunk> converter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in converter.ConvertAsync(source, cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return chunk;
    }

    Memory<TChunk> IBufferWriter<TChunk>.GetMemory(int sizeHint) => BufferMemory(sizeHint);
    Span<TChunk> IBufferWriter<TChunk>.GetSpan(int sizeHint) => BufferMemory(sizeHint).Span;
    void IBufferWriter<TChunk>.Advance(int count)
    {
        CheckBufferWriter();
        if (_staging is null || count < 0 || count > _staging.Length - _staged)
            throw new ArgumentOutOfRangeException(nameof(count));
        _staged += count;
    }

    private Memory<TChunk> BufferMemory(int sizeHint)
    {
        CheckBufferWriter();
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        var required = checked(_staged + Math.Max(1, sizeHint));
        if (_staging is null || required > _staging.Length)
        {
            var previous = _staging;
            var grown = (int)Math.Min(Array.MaxLength, Math.Max((long)required, (long)(previous?.Length ?? _capacity) * 2));
            if (grown < required) throw new OutOfMemoryException("Requested staging size exceeds the maximum array length.");
            _staging = new TChunk[grown];
            if (previous is not null)
            {
                Array.Copy(previous, _staging, _staged);
                Array.Clear(previous);
            }
        }
        return _staging.AsMemory(_staged);
    }

    private void CheckBufferWriter()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_bound || _flushing || Volatile.Read(ref _stopped) != 0)
            throw new InvalidOperationException("The manual writer is unavailable while piping, flushing or after termination.");
    }

    /// <summary>Publishes staged IBufferWriter items, waiting when the queue is full.</summary>
    /// <param name="cancellationToken">Cancels the flush.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        CheckBufferWriter();
        _flushing = true;
        try
        {
            for (var i = 0; i < _staged; i++)
                await base.WriteAsync(_staging![i], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) { Fault(error); throw; }
        finally
        {
            if (_staging is not null) Array.Clear(_staging, 0, _staged);
            _staged = 0;
            _flushing = false;
        }
    }

    /// <summary>Completes manual input after all staged items have been flushed.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new void Complete()
    {
        if (_staged != 0 || _flushing)
            throw new InvalidOperationException("Flush staged items before completing input.");
        base.Complete();
    }

    internal override void EndDispatch(Exception? exception = null)
    {
        base.EndDispatch(exception);
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            // User cancellation callbacks must not replace the dispatch's terminal error.
            try { _producerCancellation?.Cancel(); }
            catch (AggregateException) { }
        }
    }

    internal override async ValueTask WaitForProducerAsync()
    {
        if (_producer is not null) await _producer.ConfigureAwait(false);
    }

    internal override async ValueTask DisposeCoreAsync()
    {
        await base.DisposeCoreAsync().ConfigureAwait(false);
        _producerCancellation?.Dispose();
        if (_staging is not null) Array.Clear(_staging);
    }
}
