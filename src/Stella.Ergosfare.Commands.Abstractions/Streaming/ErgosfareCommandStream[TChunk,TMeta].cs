using Stella.Ergosfare.Core.Abstractions;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Streaming;

namespace Stella.Ergosfare.Commands.Abstractions.Streaming;

/// <summary>
/// A command whose payload arrives in chunks and which returns nothing.
/// </summary>
/// <typeparam name="TChunk">The chunk type the command carries.</typeparam>
/// <typeparam name="TMeta">
/// What is known before the first chunk moves — a file name, a declared length, a content
/// type.
/// </typeparam>
/// <remarks>
/// The metadata is the half of the message that exists up front, and that is what makes it
/// worth naming: the stages that run before the handler see it and nothing else, so a
/// four-gigabyte upload can be refused without a byte of it being read. The chunks belong to
/// the handler alone.
/// </remarks>
[Experimental(ExperimentalIds.StreamingSurface)]
public abstract class ErgosfareCommandStream<TChunk, TMeta> : ErgosfareStream<TChunk>, ICommand
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
    /// Settable because the pre stage normalises it in place — a stream message cannot be
    /// replaced the way an ordinary message can, since the caller is already writing into
    /// this instance.
    /// </remarks>
    public TMeta Meta { get; set; }
}
