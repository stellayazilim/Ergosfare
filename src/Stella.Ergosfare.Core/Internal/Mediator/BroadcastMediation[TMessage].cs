using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Delivers a message to every matched handler: the runtime filling of a broadcast pipeline,
/// for the dispatches a compiled plan did not claim.
/// </summary>
/// <remarks>
/// This is the sibling of the sole-handler mediation, and the two differ in exactly three
/// lines — which handlers run. Everything around them is the same: pre stages that may replace
/// the message, post stages over the resultless <see cref="Unit"/> slot, exception stages that
/// swallow only when one actually matched, and final stages an abort skips. It lives here,
/// next to that sibling's future home, because merging them is the point: a sole-handler
/// pipeline is the N = 1 case of this one.
/// </remarks>
internal static class BroadcastMediation<TMessage>
    where TMessage : notnull
{
    /// <summary>
    /// The runtime lane: the publish the compiled plan did not claim — because the event has
    /// none, because the live composition is not the one it was baked against, or because the
    /// publish selected groups. Picks the cheapest shape the pipeline allows.
    /// </summary>
    /// <remarks>
    /// This is where the broadcast mediation strategy used to live. Folding it in removes a
    /// layer and, with it, the per-publish strategy instance a non-null settings object used
    /// to allocate; what remains is one decision and one loop.
    /// </remarks>
    internal static ValueTask Deliver(
        TMessage message,
        IMessageDependencies? dependencies,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        bool throwIfNoHandlerFound)
    {
        if (dependencies is null)
        {
            return NoPipeline(throwIfNoHandlerFound);
        }

        if (dependencies is MessageDependencies { HasNoInterceptors: true } fast)
        {
            return PublishStraightThrough(message, fast, context, serviceProvider, throwIfNoHandlerFound);
        }

        return PublishThroughStages(message, dependencies, context, serviceProvider, throwIfNoHandlerFound);
    }

    /// <summary>
    /// The full interceptor-bearing broadcast: pre stages (which may replace the event),
    /// every handler in order, post stages over the resultless <see cref="Unit"/> slot,
    /// exception stages that swallow only when one actually matched, and final stages an
    /// abort skips.
    /// </summary>
    private static async ValueTask PublishThroughStages(
        TMessage message,
        IMessageDependencies dependencies,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        bool throwIfNoHandlerFound)
    {
        var handlers = dependencies.Handlers;
        var indirectHandlers = dependencies.IndirectHandlers;

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
        {
            if (throwIfNoHandlerFound)
            {
                throw new NoHandlerFoundException(typeof(TMessage));
            }

            return;
        }

        Exception? exception = null;
        var aborted = false;

        try
        {
            // Empty stages are skipped outright — an invoker pass over an empty stage is a
            // no-op, so the guards only cut dead work, not behavior.
            if (dependencies.PreInterceptors.Count > 0)
            {
                // Pre-interceptors may transform the event — including returning a brand new
                // instance — so the broadcast continues with the returned one, exactly as the
                // single-handler pipelines do. Events carry no result adapter.
                message = (TMessage)await PreInterceptorInvocationStrategy<TMessage>.Invoke(
                    dependencies, serviceProvider, message, context);
            }

            for (var i = 0; i < handlers.Count; i++)
            {
                await Invoke(handlers[i], message, context, serviceProvider);
            }

            for (var i = 0; i < indirectHandlers.Count; i++)
            {
                await Invoke(indirectHandlers[i], message, context, serviceProvider);
            }

            if (dependencies.PostInterceptors.Count > 0)
            {
                // A publish produces nothing, so the result slot carries the one value a
                // resultless pipeline has — the same Unit the void plans hand their stages.
                _ = await PostInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                    dependencies, null, serviceProvider, message, Unit.Value, context);
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the publish. Nothing else runs — not the exception
            // stage, not the final stage — and the signal continues to the publisher.
            aborted = true;
            throw;
        }
        catch (Exception e)
        {
            exception = e;

            // Zero exception interceptors: rethrow directly. Final interceptors still run
            // from the finally block.
            if (dependencies.ExceptionInterceptors.Count == 0)
            {
                throw;
            }

            var (matched, _) = await ExceptionInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                dependencies, serviceProvider, message, Unit.Value, e, context);

            // Every registered interceptor filtered the exception out — nothing handled it,
            // so it propagates with its original stack.
            if (!matched)
            {
                throw;
            }
        }
        finally
        {
            if (dependencies.FinalInterceptors.Count > 0 && !aborted)
            {
                await FinalInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                    dependencies, serviceProvider, message, Unit.Value, exception, context);
            }
        }
    }

    /// <summary>
    /// Broadcasts sequentially over the direct then indirect handler arrays without any
    /// async machinery while handlers complete synchronously; the first suspension hands
    /// the remainder to an awaiting helper, preserving strict sequential order. Semantics
    /// match the interceptor-bearing lane for the zero-interceptor case: exceptions
    /// propagate raw, and an empty pipeline throws only when
    /// <paramref name="throwIfNoHandlerFound"/> asks for it.
    /// </summary>
    private static ValueTask PublishStraightThrough(
        TMessage message,
        MessageDependencies plan,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        bool throwIfNoHandlerFound)
    {
        var direct = plan.HandlerArray;
        var indirect = plan.IndirectHandlerArray;

        if (direct.Length == 0 && indirect.Length == 0)
        {
            return throwIfNoHandlerFound
                ? ValueTask.FromException(new NoHandlerFoundException(typeof(TMessage)))
                : default;
        }

        for (var i = 0; i < direct.Length; i++)
        {
            var pending = Invoke(direct[i], message, context, serviceProvider);

            if (!pending.IsCompletedSuccessfully)
            {
                return AwaitRemaining(pending, message, plan, context, serviceProvider, i + 1, inIndirect: false);
            }
        }

        for (var i = 0; i < indirect.Length; i++)
        {
            var pending = Invoke(indirect[i], message, context, serviceProvider);

            if (!pending.IsCompletedSuccessfully)
            {
                return AwaitRemaining(pending, message, plan, context, serviceProvider, i + 1, inIndirect: true);
            }
        }

        return default;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitRemaining(
            ValueTask pending, TMessage message, MessageDependencies plan, ErgosfareContext context,
            IServiceProvider serviceProvider, int next, bool inIndirect)
        {
            await pending;

            var direct = plan.HandlerArray;
            var indirect = plan.IndirectHandlerArray;

            if (!inIndirect)
            {
                for (var i = next; i < direct.Length; i++)
                {
                    await Invoke(direct[i], message, context, serviceProvider);
                }

                next = 0;
            }

            for (var i = next; i < indirect.Length; i++)
            {
                await Invoke(indirect[i], message, context, serviceProvider);
            }
        }
    }

    /// <summary>
    /// Invokes one handler through its typed contract — the same dispatch rules the
    /// broadcast strategy applies.
    /// </summary>
    private static ValueTask Invoke(
        IHandlerReference<Core.Abstractions.Handlers.IHandler> reference,
        TMessage message,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        var handler = reference.Resolve(serviceProvider);

        switch (handler)
        {
            case Core.Abstractions.Handlers.IAsyncHandler<TMessage> asyncHandler:
                return asyncHandler.HandleAsync(message, context);
            case Core.Abstractions.Handlers.IHandler<TMessage, ValueTask> valueTaskShaped:
                return valueTaskShaped.Handle(message, context);
            case Core.Abstractions.Handlers.IHandler<TMessage, object> syncHandler:
                syncHandler.Handle(message, context);
                return ValueTask.CompletedTask;
            default:
                throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported handler contract for event '{typeof(TMessage)}'. " +
                    "Interface-erased dispatch is not supported; publish with the concrete event type.");
        }
    }

    /// <summary>
    /// The publish outcome for an event nothing will handle: the caller's flag decides,
    /// and it decides the same way whether the event type is unregistered or merely
    /// unhandled.
    /// </summary>
    private static ValueTask NoPipeline(bool throwIfNoHandlerFound)
        => throwIfNoHandlerFound
            ? ValueTask.FromException(new NoHandlerFoundException(typeof(TMessage)))
            : default;
}
