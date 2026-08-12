using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Abstractions.Strategies;

/// <summary>
/// Implements a mediation strategy for a single asynchronous handler.
/// Ensures that only one handler is executed for the message, invokes pre- and post-interceptors,
/// handles exceptions, and applies final interceptors. Supports optional result adaptation.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the handler.</typeparam>
public sealed class SingleAsyncHandlerMediationStrategy<TMessage, TResult> : IMessageMediationStrategy<TMessage, ValueTask<TResult>> 

    where TMessage : notnull
{
    // The effective adapter of this pipeline's closed result slot — the attribute tiers
    // plus the container's configured default — resolved once on the first dispatch (the
    // default tier needs the provider) and published through the volatile flag. Null, the
    // overwhelmingly common case, means the dispatch path performs no probing at all.
    private IResultAdapter<TResult>? _resultAdapter;

    // The adapter's materializer facet, when the carrier can absorb a failure: a real
    // throw is then caught and materialized into a failed carrier instead of reaching the
    // caller, and an unhandled carried failure flows out as the result — choosing a
    // materializable carrier type is choosing throwlessness.
    private IResultMaterializer<TResult>? _resultMaterializer;

    private volatile bool _resultAdapterResolved;

    /// <summary>
    /// Resolves the slot's effective adapter once. The container is sealed after build,
    /// so the resolution can never change; a duplicate-resolution race is benign — both
    /// writers publish equivalent state, and the volatile flag orders the publication.
    /// </summary>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = Results.ResultAdapterBinding.For<TMessage, TResult>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<TResult>;
        _resultAdapterResolved = true;
    }


    /// <summary>
    /// Mediates the message by invoking the single registered handler along with the pre-,
    /// post-, exception- and final-interceptor stages, applying optional result adaptation.
    /// </summary>
    /// <param name="message">The message to be handled. May be transformed by pre-interceptors.</param>
    /// <param name="messageDependencies">The dependencies of the message, including the registered handler and interceptor stages.</param>
    /// <param name="context">The current execution context.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; handlers and interceptors resolve from it.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, returning the final result
    /// after executing the handler and all applicable interceptors.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageDependencies"/> is null.</exception>
    /// <exception cref="MultipleHandlerFoundException">Thrown if more than one handler is registered for the message.</exception>
    /// <exception cref="NoHandlerFoundException">Thrown if no handler is registered for the message.</exception>
    /// <remarks>
    /// <para>The mediation process follows this sequence:</para>
    /// <list type="number">
    /// <item>With no interceptors registered, the handler is invoked directly on a fast path
    /// and exceptions propagate unchanged.</item>
    /// <item>Pre-interceptors run via <see cref="PreInterceptorInvocationStrategy{TMessage}"/>;
    /// each may transform the message.</item>
    /// <item>The main handler runs through its typed contract; the result adapter may surface
    /// a failure carried inside the result — the value channel — which enters the exception
    /// stage without a throw.</item>
    /// <item>Post-interceptors run via <see cref="PostInterceptorInvocationStrategy{TMessage, TResult}"/>
    /// and may replace the result; each post result is probed the same way.</item>
    /// <item>On failure — thrown or carried — <see cref="ExceptionInterceptorInvocationStrategy{TMessage, TResult}"/>
    /// runs. An unhandled failure is materialized into a failed carrier when the result
    /// type's adapter can absorb one; otherwise it is (re)thrown after the final stage. An
    /// <see cref="ExecutionAbortedException"/> aborts without error.</item>
    /// <item><see cref="FinalInterceptorInvocationStrategy{TMessage, TResult}"/> always runs last,
    /// regardless of success or failure.</item>
    /// </list>
    /// </remarks>
    public async ValueTask<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }

        EnsureResultAdapter(serviceProvider);

        // The main-handler priority ladder; see the void strategy for the reasoning.
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
                fastResult = await InvokeHandler(fastHandler, message, context);
            }
            catch (ExecutionAbortedException)
            {
                // The handler short-circuited before producing anything, so there is
                // nothing to hand back but the result type's default.
                return default!;
            }
            catch (Exception e) when (_resultMaterializer is not null)
            {
                // Catch-materialization: a materializable carrier type never lets a real
                // throw reach the caller — the failure comes back inside the carrier.
                return _resultMaterializer.Materialize(e);
            }

            // A carried failure with nobody to tell: no interceptor stages exist here. A
            // materializable carrier flows out as-is for the caller to inspect; any other
            // carrier keeps the classic contract — an unhandled failure surfaces as a throw.
            if (_resultMaterializer is null
                && _resultAdapter is not null
                && _resultAdapter.TryGetException(in fastResult, out var fastEx) && fastEx is not null)
            {
                throw fastEx;
            }

            return fastResult;
        }

        TResult result = default!;
        Exception? exception = null;
        ExceptionDispatchInfo? unhandledException = null;
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
                if (_resultAdapter is not null && _resultAdapter.TryGetException(in result, out var carried) && carried is not null)
                {
                    exception = carried;
                }

                if (exception is null && postInterceptorCount > 0)
                {
                    var (postResult, postCarried) = await PostInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                        messageDependencies, _resultAdapter, serviceProvider, message, result, context);

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
            catch (ExecutionAbortedException)
            {
                // A short circuit, not a failure: the exception stage is skipped, the caller
                // sees no exception, and `result` — whatever the pipeline had produced by the
                // time the abort unwound — is what the final stage and the caller both get.
            }
            catch (Exception e)
            {
                // The classic zero-interceptor, no-adapter rethrow keeps its exact shape.
                if (exceptionInterceptorCount == 0 && _resultAdapter is null)
                {
                    exception = e;
                    throw;
                }

                exception = e;

                if (_resultMaterializer is not null)
                {
                    // Catch-materialization: the failure is absorbed into a failed carrier
                    // before the exception stage sees it — a declining stage then leaves
                    // the materialized failure standing, and the caller never sees a throw.
                    result = _resultMaterializer.Materialize(e);
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
                        var typedStageResult = (TResult?)stageResult;
                        result = typedStageResult is null ? result : typedStageResult;
                    }
                }

                if (matched)
                {
                    unhandledException = null;
                }
                else if (_resultMaterializer is null)
                {
                    // Nobody accepted the failure and the carrier cannot absorb one: it
                    // surfaces as a throw — after the final stage, like every unhandled
                    // failure. A carried failure was never thrown, so capturing it here
                    // is where its dispatch stack begins.
                    unhandledException ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        finally
        {
            if (finalInterceptorCount > 0)
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
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handlers declare notnull results
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
#pragma warning restore CS8714
}