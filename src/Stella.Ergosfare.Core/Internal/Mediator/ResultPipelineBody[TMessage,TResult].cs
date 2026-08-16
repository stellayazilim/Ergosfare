using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The result-producing pipeline's runtime body; the counterpart of
/// <see cref="VoidPipelineBody{TMessage}"/> with a real result slot: the value channel can
/// carry a failure inside the result, post and exception stages may replace the result, and
/// a materializable carrier absorbs failures instead of throwing.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal static class ResultPipelineBody<TMessage, TResult> where TMessage : notnull
{
    /// <summary>
    /// Runs the pipeline; see <see cref="VoidPipelineBody{TMessage}.Run"/> for the stage
    /// contract — the differences here are the typed result slot and its adaptation.
    /// </summary>
    internal static async ValueTask<TResult> Run(
        TMessage message,
        IMessageDependencies messageDependencies,
        IResultAdapter<TResult>? resultAdapter,
        IResultMaterializer<TResult>? resultMaterializer,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(messageDependencies);

        // The main-handler priority ladder; see the void body for the reasoning.
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
            // Typed seam: when the dispatch TMessage is the handler's message type (or a
            // derived one — IHandler's `in TMessage` variance covers that), invoke the typed
            // member directly and skip the object-typed DIM bridge. Interface-erased
            // dispatches (TMessage = ICommand<T> etc.) fall back to the bridge.
            var fastHandler = soleHandler.Resolve(serviceProvider);

            TResult fastResult;

            try
            {
                // No abort arm here: a handler that stops its own dispatch has nothing left
                // to tell and no stage left to skip, so the signal travels to the caller.
                fastResult = await InvokeHandler(fastHandler, message, context);
            }
            catch (Exception e) when (resultMaterializer is not null && e is not ExecutionAbortedException)
            {
                // Catch-materialization: a materializable carrier type never lets a real
                // throw reach the caller — the failure comes back inside the carrier.
                return resultMaterializer.Materialize(e);
            }

            // A carried failure with nobody to tell: no interceptor stages exist here. A
            // materializable carrier flows out as-is for the caller to inspect; any other
            // carrier keeps the classic contract — an unhandled failure surfaces as a throw.
            if (resultMaterializer is null
                && resultAdapter is not null
                && resultAdapter.TryGetException(in fastResult, out var fastEx) && fastEx is not null)
            {
                throw fastEx;
            }

            return fastResult;
        }

        TResult result = default!;
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

                result = await InvokeHandler(handler, message, context);

                // The value channel: a failure carried inside the result enters the
                // exception stage below without a throw being paid anywhere.
                if (resultAdapter is not null && resultAdapter.TryGetException(in result, out var carried) && carried is not null)
                {
                    exception = carried;
                }

                if (exception is null && postInterceptorCount > 0)
                {
                    var (postResult, postCarried) = await PostInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                        messageDependencies, resultAdapter, serviceProvider, message, result, context);

                    if (postCarried is not null)
                    {
                        // The failed carrier a post-interceptor produced IS the pipeline's
                        // result from here on; the stage already skipped the remaining posts.
                        result = (TResult)postResult!;
                        exception = postCarried;
                    }
                    else
                    {
                        var typedPostResult = (TResult?)postResult;
                        result = typedPostResult is null ? result : typedPostResult;
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
                    // Catch-materialization: the failure is absorbed into a failed carrier
                    // before the exception stage sees it — a declining stage then leaves
                    // the materialized failure standing, and the caller never sees a throw.
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
                    (matched, var stageResult) = await ExceptionInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                        messageDependencies, serviceProvider, message, result, exception, context);

                    if (matched)
                    {
                        // A matched interceptor handled the failure, so its answer IS the
                        // result — there is nothing to fall back to. This used to keep the
                        // previous result when the stage returned null, which reads as
                        // defensive and is not: the handler threw, so the previous result is
                        // still `default!`, and the dispatch answered null for a type that
                        // promised a value while the failure disappeared.
                        //
                        // Nullability belongs to the message. A dispatch of ICommand<User>
                        // locked User at the call site and no stage may downgrade that. The
                        // contracts say so now; this is what an assembly compiled against the
                        // older ones meets instead of the silent default.
                        if (stageResult is null)
                        {
                            throw new InvalidOperationException(
                                $"An exception interceptor handled the failure of '{typeof(TMessage)}' and returned no result. " +
                                $"The dispatch is typed '{typeof(TResult)}' and cannot answer with null: produce a result, " +
                                "or leave the failure unhandled so it surfaces to the caller.");
                        }

                        result = (TResult)stageResult;
                    }
                }

                if (matched)
                {
                    unhandledException = null;
                }
                else if (resultMaterializer is null)
                {
                    // Nobody accepted the failure and the carrier cannot absorb one: it
                    // surfaces as a throw — after the final stage, like every unhandled
                    // failure. A carried failure was never thrown, so capturing it here
                    // is where its dispatch stack begins.
                    unhandledException ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the pipeline. Nothing else runs — not the exception
            // stage, not the final stage below — and the signal continues to the caller,
            // which is why nothing is returned from here either.
            aborted = true;
            throw;
        }
        finally
        {
            if (finalInterceptorCount > 0 && !aborted)
            {
                await FinalInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                    messageDependencies, serviceProvider, message, result, exception, context);
            }
        }

        unhandledException?.Throw();

        return result;
    }

    /// <summary>
    /// Invokes the handler through its typed contract. There is no object-typed bridge:
    /// asynchronous handlers are called via <c>IAsyncHandler</c>, synchronous handlers via
    /// <see cref="IHandler{TMessage, TResult}"/> (`in TMessage` variance admits handlers
    /// registered for base message types). Interface-erased dispatch is unsupported —
    /// dispatch with the concrete message type (the executor path) instead.
    /// </summary>
    private static ValueTask<TResult> InvokeHandler(object handler, TMessage message, ErgosfareContext context)
        => handler switch
        {
            IAsyncHandler<TMessage, TResult> asyncHandler => asyncHandler.HandleAsync(message, context),
            IHandler<TMessage, ValueTask<TResult>> valueTaskShaped => valueTaskShaped.Handle(message, context),
            IHandler<TMessage, TResult> syncHandler => ValueTask.FromResult(syncHandler.Handle(message, context)),
            _ => throw new NotSupportedException(
                $"'{handler.GetType()}' does not implement a supported handler contract for message '{typeof(TMessage)}' and result '{typeof(TResult)}'. " +
                "Interface-erased dispatch is not supported; dispatch with the concrete message type."),
        };
}
#pragma warning restore CS8714
