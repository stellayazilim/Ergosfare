using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The process-wide pool execution contexts are rented from and returned to. Dispatches
/// return their context when the pipeline completes; child scopes return theirs on dispose.
/// </summary>
/// <remarks>
/// The pool is bounded. Once it is full, returned contexts are dropped and collected
/// normally, and a context that is never returned — a caller-owned one, or one that leaked
/// — simply costs an allocation.
/// </remarks>
internal static class ErgosfareContextPool
{
    private const int MaxRetained = 128;

    /// <summary>
    /// A single per-thread slot, checked before the shared queue. A dispatch that completes
    /// synchronously rents and returns on the same thread, so it meets its context here
    /// without touching shared state. Pipelines that resume on another thread fall through
    /// to the queue.
    /// </summary>
    [ThreadStatic]
    private static ErgosfareContext? _threadSlot;

    private static readonly ConcurrentQueue<ErgosfareContext> Contexts = new();
    private static int _retained;

    /// <summary>
    /// Returns a context prepared for a new dispatch, reusing a pooled one when available.
    /// </summary>
    /// <param name="items">The caller's items dictionary, or <c>null</c> to allocate on demand.</param>
    /// <param name="cancellationToken">The token for the dispatch.</param>
    /// <returns>A context ready to dispatch under.</returns>
    public static ErgosfareContext Rent(IDictionary<object, object?>? items, CancellationToken cancellationToken)
    {
        var context = _threadSlot;

        if (context is not null)
        {
            _threadSlot = null;
            context.Reset(items, cancellationToken);
            return context;
        }

        if (Contexts.TryDequeue(out context))
        {
            Interlocked.Decrement(ref _retained);
            context.Reset(items, cancellationToken);
            return context;
        }

        return new ErgosfareContext(items, cancellationToken);
    }

    /// <summary>
    /// Clears <paramref name="context"/> and takes it back, or drops it when the pool is
    /// full.
    /// </summary>
    /// <param name="context">The context whose dispatch has completed.</param>
    public static void Return(ErgosfareContext context)
    {
        context.Clear();

        if (_threadSlot is null)
        {
            _threadSlot = context;
            return;
        }

        if (Interlocked.Increment(ref _retained) <= MaxRetained)
        {
            Contexts.Enqueue(context);
        }
        else
        {
            // Over the limit: undo the reservation and let the context be collected.
            Interlocked.Decrement(ref _retained);
        }
    }
}
