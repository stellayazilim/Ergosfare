using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>
/// The chunk-carrying half of a stream message, without its chunk type.
/// </summary>
/// <remarks>
/// What the pipeline needs of a stream message it can name without knowing what the chunks
/// are: one type test, and the end of the dispatch. The typed half —
/// <see cref="ErgosfareStream{TChunk}"/> — is what callers and handlers use.
/// </remarks>
[Experimental(ExperimentalIds.StreamingSurface)]
public abstract class ErgosfareStream : IMessage
{
    /// <summary>
    /// Tells the stream its dispatch is over.
    /// </summary>
    /// <remarks>
    /// A pipeline that stops has to end the stream with it. The two are separate
    /// synchronisation objects: when a stage refuses or a handler throws, nothing about that
    /// reaches a caller waiting on a full buffer, and it would wait for a reader that is
    /// never coming — the write is not slow, it is abandoned. Internal because it is the
    /// dispatch's job and nobody else's: a producer that wants to end its own stream has
    /// <c>Complete</c> and <c>Fault</c>.
    /// </remarks>
    internal abstract void EndDispatch();
}
