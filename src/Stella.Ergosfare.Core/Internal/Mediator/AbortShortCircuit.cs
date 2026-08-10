using Stella.Ergosfare.Core.Abstractions.Exceptions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The executors' half of the abort contract. A pipeline with no interceptor stages is
/// invoked handler-first, straight from the executor, with the mediation strategy — and its
/// abort handling — skipped entirely. Only the handler itself can short-circuit such a
/// dispatch, and nothing was produced when it does, so the caller gets the result type's
/// default and no exception.
/// </summary>
/// <remarks>
/// A synchronous throw never reaches here: the executors catch that around the invocation
/// itself. This covers the other half, an abort captured into the returned task, and it
/// pays only when the handler did not complete synchronously — the hot path returns without
/// building a state machine.
/// </remarks>
internal static class AbortShortCircuit
{
    /// <summary>Completes normally if <paramref name="task"/> aborted.</summary>
    public static ValueTask Guard(ValueTask task)
        => task.IsCompletedSuccessfully ? default : Awaited(task);

    /// <summary>Yields <c>default</c> if <paramref name="task"/> aborted.</summary>
    public static ValueTask<TResult> Guard<TResult>(ValueTask<TResult> task)
        => task.IsCompletedSuccessfully ? task : Awaited(task);

    private static async ValueTask Awaited(ValueTask task)
    {
        try
        {
            await task;
        }
        catch (ExecutionAbortedException)
        {
        }
    }

    private static async ValueTask<TResult> Awaited<TResult>(ValueTask<TResult> task)
    {
        try
        {
            return await task;
        }
        catch (ExecutionAbortedException)
        {
            return default!;
        }
    }
}
