using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The void pipeline's runtime body: the plan family's N = 1 base case, run over a frozen
/// composition. Hosts the exact semantics the single-handler mediation strategy carried —
/// the main-handler priority ladder, the zero-interceptor fast path, the result-slot value
/// channel, exception matching, and the abort/final contract — as a static body the frozen
/// dispatches call with their own frozen state. There is no strategy instance and nothing
/// here decides per dispatch; every input is resolved by the caller, once.
/// </summary>
internal static class VoidPipelineBody<TMessage> where TMessage : IMessage
{
    /// <summary>
    /// Runs the pipeline: pre stages (which may replace the message), the sole main
    /// handler, post stages over the resultless <see cref="Unit"/> slot, exception stages
    /// that swallow only when one actually matched, and final stages an abort skips.
    /// </summary>
    internal static async ValueTask Run(
        TMessage message,
        IMessageDependencies messageDependencies,
        IResultAdapter<Unit>? resultAdapter,
        IResultMaterializer<Unit>? resultMaterializer,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(messageDependencies);

        // The main-handler priority ladder: the direct level wins outright — a sole
        // direct handler serves the message no matter how many covariant candidates
        // exist (a covariant handler is a fallback, not a competitor); without a direct
        // one the dispatch falls to the covariant level. More than one candidate AT THE
        // SAME LEVEL is a contest, counted before anything resolves so a contested
        // message runs neither claimant.
        var handlers = messageDependencies.Handlers;
        var indirectHandlers = messageDependencies.IndirectHandlers;

        if (handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), handlers.Count);
        }

        if (handlers.Count == 0 && indirectHandlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), indirectHandlers.Count);
        }

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
        {
            throw new NoHandlerFoundException(typeof(TMessage), $"No handler is registered for {typeof(TMessage).Name}.");
        }

        var soleHandler = handlers.Count == 1 ? handlers[0] : indirectHandlers[0];

        var preInterceptorCount = messageDependencies.PreInterceptors.Count;
        var postInterceptorCount = messageDependencies.PostInterceptors.Count;
        var exceptionInterceptorCount = messageDependencies.ExceptionInterceptors.Count;
        var finalInterceptorCount = messageDependencies.FinalInterceptors.Count;

        // Fast path: with no interceptors registered, none of the invocation strategies can
        // observe or transform anything — invoke the handler directly. Exceptions propagate
        // unchanged, matching the zero-interceptor rethrow behavior of the full pipeline.
        if ((preInterceptorCount | postInterceptorCount | exceptionInterceptorCount | finalInterceptorCount) == 0)
        {
            // Typed seam: direct typed invocation when the dispatch TMessage satisfies the
            // handler's message type (`in TMessage` variance; `out TResult` admits ValueTask<T>
            // for a ValueTask slot). Interface-erased dispatches fall back to the DIM bridge.
            var fastHandler = soleHandler.Resolve(serviceProvider);

            try
            {
                // No abort arm here: a handler that stops its own dispatch has nothing left
                // to tell and no stage left to skip, so the signal travels to the caller.
                await InvokeHandler(fastHandler, message, context);
            }
            catch (Exception e) when (resultMaterializer is not null && e is not ExecutionAbortedException)
            {
                // Catch-materialization; a void pipeline has nothing to hand back, so the
                // materialized carrier is the absorption itself.
                resultMaterializer.Materialize(e);
                return;
            }

            if (resultMaterializer is null
                && resultAdapter is not null
                && resultAdapter.TryGetException(in Unit.Value, out var fastEx) && fastEx is not null)
            {
                throw fastEx;
            }

            return;
        }

        // The handler's ValueTask is the completion signal and is consumed here; what flows
        // on through the interceptor stages is the result slot, and a void pipeline has one
        // value for it — Unit.Value once the handler has run, null before that.
        Unit? result = null;
        Exception? exception = null;
        ExceptionDispatchInfo? unhandledException = null;
        var aborted = false;
        try
        {
            try
            {
                if (preInterceptorCount > 0)
                {
                    message = (TMessage) await PreInterceptorInvocationStrategy<TMessage>.Invoke(
                        messageDependencies, serviceProvider, message, context);
                }

                var handler = soleHandler.Resolve(serviceProvider);

                await InvokeHandler(handler, message, context);
                result = Unit.Value;

                // The value channel; see the result-producing body.
                if (resultAdapter is not null && resultAdapter.TryGetException(in Unit.Value, out var carried) && carried is not null)
                {
                    exception = carried;
                }

                if (exception is null && postInterceptorCount > 0)
                {
                    var (postResult, postCarried) = await PostInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                        messageDependencies, resultAdapter, serviceProvider, message, result, context);

                    if (postCarried is not null)
                    {
                        exception = postCarried;
                    }
                    else
                    {
                        result = (Unit?)postResult ?? result;
                    }
                }
            }
            catch (Exception e) when (e is not ExecutionAbortedException)
            {
                // The classic zero-interceptor, no-adapter rethrow keeps its exact shape.
                if (exceptionInterceptorCount == 0 && resultAdapter is null)
                {
                    exception = e;
                    throw;
                }

                exception = e;

                if (resultMaterializer is not null)
                {
                    result = resultMaterializer.Materialize(e);
                }
                else
                {
                    unhandledException = ExceptionDispatchInfo.Capture(e);
                }
            }

            if (exception is not null)
            {
                var matched = false;

                if (exceptionInterceptorCount > 0)
                {
                    (matched, var stageResult) = await ExceptionInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                        messageDependencies, serviceProvider, message, result, exception, context);

                    if (matched)
                    {
                        result = (Unit?)stageResult ?? result;
                    }
                }

                if (matched)
                {
                    unhandledException = null;
                }
                else if (resultMaterializer is null)
                {
                    // Nobody accepted the failure and nothing absorbs it: it surfaces as a
                    // throw after the final stage, like every unhandled failure.
                    unhandledException ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the pipeline. Nothing else runs — not the exception
            // stage, not the final stage below — and the signal continues to the caller.
            aborted = true;
            throw;
        }
        finally
        {
            if (finalInterceptorCount > 0 && !aborted)
            {
                await FinalInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                    messageDependencies, serviceProvider, message, result, exception, context);
            }
        }

        unhandledException?.Throw();
    }

    /// <summary>
    /// Invokes the handler through its typed contract — no object-typed bridge; see the
    /// result-producing body for the dispatch rules.
    /// </summary>
    private static ValueTask InvokeHandler(object handler, TMessage message, ErgosfareContext context)
    {
        switch (handler)
        {
            case IAsyncHandler<TMessage> asyncHandler:
                return asyncHandler.HandleAsync(message, context);
            case IHandler<TMessage, ValueTask> valueTaskShaped:
                return valueTaskShaped.Handle(message, context);
            case IHandler<TMessage, object> syncHandler:
                syncHandler.Handle(message, context);
                return ValueTask.CompletedTask;
            default:
                throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported handler contract for message '{typeof(TMessage)}'. " +
                    "Interface-erased dispatch is not supported; dispatch with the concrete message type.");
        }
    }
}
