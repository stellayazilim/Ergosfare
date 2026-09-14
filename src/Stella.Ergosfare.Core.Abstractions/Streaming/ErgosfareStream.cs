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
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public abstract class ErgosfareStream : IMessage, IAsyncDisposable
{
    private readonly object _disposeGate = new();
    private Task? _disposeTask;

    /// <summary>Whether input disposal has started.</summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>Stops the input and awaits owned producer cleanup. Repeated calls share completion.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            IsDisposed = true;
            return new ValueTask(_disposeTask ??= DisposeCoreAsync().AsTask());
        }
    }

    internal virtual ValueTask DisposeCoreAsync()
    {
        EndDispatch(new Exceptions.StreamOutputDisposedException());
        return WaitForProducerAsync();
    }

    /// <summary>
    /// Tells the stream its dispatch is over.
    /// </summary>
    /// <param name="exception">The terminal failure, if any.</param>
    /// <remarks>
    /// A pipeline that stops has to end the stream with it. The two are separate
    /// synchronisation objects: when a stage refuses or a handler throws, nothing about that
    /// reaches a caller waiting on a full buffer, and it would wait for a reader that is
    /// never coming — the write is not slow, it is abandoned. Internal because it is the
    /// dispatch's job and nobody else's: a producer that wants to end its own stream has
    /// <c>Complete</c> and <c>Fault</c>.
    /// </remarks>
    internal abstract void EndDispatch(Exception? exception = null);

    internal virtual ValueTask WaitForProducerAsync() => default;
}
