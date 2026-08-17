using Stella.Ergosfare.Core.Abstractions.Streaming;

namespace Stella.Ergosfare.Queries.Abstractions.Streaming;

/// <summary>
/// A query whose payload arrives in chunks and which answers with a
/// <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TChunk">The chunk type the query carries.</typeparam>
/// <typeparam name="TMeta">What is known before the first chunk moves.</typeparam>
/// <typeparam name="TResult">The type the handler answers with.</typeparam>
/// <remarks>
/// The same shape the command side has, and deliberately so: streaming is a property of the
/// message, not of the module it belongs to. A query that has to read its input in pieces —
/// a search over an uploaded file, a checksum, a validation pass — is written here and
/// dispatched with the ordinary query verb.
/// </remarks>
public abstract class ErgosfareQueryStream<TChunk, TMeta, TResult> : ErgosfareStream<TChunk>, IQuery<TResult>
    where TMeta : notnull
{
    /// <summary>
    /// Creates a query stream the caller writes into.
    /// </summary>
    /// <param name="meta">What is known before the chunks move.</param>
    /// <param name="capacity">How many chunks may be buffered before a write waits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
    protected ErgosfareQueryStream(TMeta meta, int capacity = DefaultCapacity)
        : base(capacity)
        => Meta = meta ?? throw new ArgumentNullException(nameof(meta));

    /// <summary>
    /// Creates a query stream over a source that already exists.
    /// </summary>
    /// <param name="meta">What is known before the chunks move.</param>
    /// <param name="source">The sequence to hand to the handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
    protected ErgosfareQueryStream(TMeta meta, IAsyncEnumerable<TChunk> source)
        : base(source)
        => Meta = meta ?? throw new ArgumentNullException(nameof(meta));

    /// <summary>
    /// What is known about the stream before it moves.
    /// </summary>
    /// <remarks>
    /// Settable because the pre stage normalises it in place; the instance itself cannot be
    /// swapped while the caller is writing into it.
    /// </remarks>
    public TMeta Meta { get; set; }
}
