using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Internal.Contexts;

/// <summary>
/// Process-wide pool of execution contexts. A dispatch rents a context and returns it
/// when the pipeline completes; child scopes return theirs on dispose. Contexts are pure
/// data carriers, so pooling them removes the dominant remaining per-dispatch allocation.
/// The pool is bounded — under a burst the overflow contexts are simply dropped to the GC,
/// and a context that is never returned (caller-owned or leaked) costs exactly what an
/// unpooled context did.
/// </summary>
internal static class ErgosfareExecutionContextPool
{
    private const int MaxRetained = 128;

    /// <summary>
    /// Per-thread fast slot: the overwhelmingly common dispatch completes synchronously on
    /// the thread it started on, so rent and return meet here for a plain load/store —
    /// no interlocked traffic. Suspended pipelines that complete on another thread fall
    /// through to the shared queue.
    /// </summary>
    [ThreadStatic]
    private static ErgosfareExecutionContext? _threadSlot;

    private static readonly ConcurrentQueue<ErgosfareExecutionContext> Contexts = new();
    private static int _retained;

    /// <summary>Rents a context initialized with the given items and cancellation token.</summary>
    public static ErgosfareExecutionContext Rent(IDictionary<object, object?>? items, CancellationToken cancellationToken)
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

        return new ErgosfareExecutionContext(items, cancellationToken);
    }

    /// <summary>Clears and returns a context; drops it when the pool is full.</summary>
    public static void Return(ErgosfareExecutionContext context)
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
            Interlocked.Decrement(ref _retained);
        }
    }
}
