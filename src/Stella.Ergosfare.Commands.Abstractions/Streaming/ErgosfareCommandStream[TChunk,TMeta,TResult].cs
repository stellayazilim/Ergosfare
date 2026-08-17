using Stella.Ergosfare.Core.Abstractions;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Streaming;

namespace Stella.Ergosfare.Commands.Abstractions.Streaming;

/// <summary>
/// A command whose payload arrives in chunks and which returns a
/// <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TChunk">The chunk type the command carries.</typeparam>
/// <typeparam name="TMeta">What is known before the first chunk moves.</typeparam>
/// <typeparam name="TResult">The type the handler returns.</typeparam>
/// <remarks>
/// <para>
/// The two axes are independent: the chunks are how the message arrives, the result is what
/// comes back. A converter takes a video in chunks and answers with a small report — the
/// payload never exists as one value in either direction, and the pipeline around it is the
/// ordinary one.
/// </para>
/// <para>
/// A result that itself streams is written as <c>IAsyncEnumerable&lt;T&gt;</c> here; it needs
/// no separate contract, and it is the one shape where the stages after the handler have no
/// result to work with.
/// </para>
/// </remarks>
[Experimental(ExperimentalIds.StreamingSurface)]
public abstract class ErgosfareCommandStream<TChunk, TMeta, TResult> : ErgosfareStream<TChunk>, ICommand<TResult>
    where TMeta : notnull
{
    /// <summary>
    /// Creates a command stream the caller writes into.
    /// </summary>
    /// <param name="meta">What is known before the chunks move.</param>
    /// <param name="capacity">How many chunks may be buffered before a write waits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
    protected ErgosfareCommandStream(TMeta meta, int capacity = DefaultCapacity)
        : base(capacity)
        => Meta = meta ?? throw new ArgumentNullException(nameof(meta));

    /// <summary>
    /// Creates a command stream over a source that already exists.
    /// </summary>
    /// <param name="meta">What is known before the chunks move.</param>
    /// <param name="source">The sequence to hand to the handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="meta"/> is <c>null</c>.</exception>
    protected ErgosfareCommandStream(TMeta meta, IAsyncEnumerable<TChunk> source)
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
