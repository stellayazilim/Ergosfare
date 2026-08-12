using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Abstractions.Strategies;

/// <summary>
///     Represents a mediation strategy that processes a message through a single asynchronous handler.
/// </summary>
/// <typeparam name="TMessage">The type of message being mediated.</typeparam>
/// <remarks>
///     This strategy ensures that only one handler is registered for the message type and then:
///     1. Executes pre-handlers.
///     2. Delegates the message processing to the registered handler.
///     3. Executes post-handlers.
///     In case of any exception during the process, it delegates the error handling to the registered error handlers.
/// </remarks>
public sealed class SingleAsyncHandlerMediationStrategy<TMessage> : IMessageMediationStrategy<TMessage, ValueTask> where TMessage : IMessage
{
    // The effective adapter of the void pipeline's Unit slot — resolved once on the
    // first dispatch (the default tier needs the provider) and published through the
    // volatile flag; null unless a Unit adapter is deliberately bound.
    private IResultAdapter<Unit>? _resultAdapter;

    // The adapter's materializer facet; see the result-producing strategy. A Unit
    // materializer absorbs pipeline failures into the void pipeline's non-result.
    private IResultMaterializer<Unit>? _resultMaterializer;

    private volatile bool _resultAdapterResolved;

    /// <summary>
    /// Resolves the slot's effective adapter once; see the result-producing strategy for
    /// the publication reasoning.
    /// </summary>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = Results.ResultAdapterBinding.For<TMessage, Unit>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<Unit>;
        _resultAdapterResolved = true;
    }

    /// <summary>
    ///     Mediates a message by executing the appropriate handler and orchestrating the handling pipeline.
    /// </summary>
    /// <param name="message">The message to be mediated.</param>
    /// <param name="messageDependencies">
    ///     The dependencies required for message handling, including the handler and the
    ///     pre-, post-, exception- and final-interceptor stages.
    /// </param>
    /// <param name="context">
    ///     The context in which the mediation is executed, providing access to cancellation tokens,
    ///     shared data, and other execution-related information.
    /// </param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; handlers and interceptors resolve from it.</param>
    /// <returns>A task representing the asynchronous mediation operation.</returns>
    /// <exception cref="MultipleHandlerFoundException">Thrown when more than one handler is found for the message type.</exception>
    /// <exception cref="NoHandlerFoundException">Thrown when no handler is registered for the message type.</exception>
    /// <remarks>
    ///     Pre-interceptors, the main handler and post-interceptors run in sequence; with no
    ///     interceptors registered the handler is invoked directly on a fast path. If an
    ///     exception occurs, the exception interceptors run; final interceptors always run.
    ///     An <see cref="ExecutionAbortedException" /> stops the pipeline outright — no
    ///     remaining stage runs, finals included — and travels to the caller.
    /// </remarks>
    public async ValueTask Mediate(TMessage message, IMessageDependencies messageDependencies, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }

        EnsureResultAdapter(serviceProvider);

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
            catch (Exception e) when (_resultMaterializer is not null && e is not ExecutionAbortedException)
            {
                // Catch-materialization; a void pipeline has nothing to hand back, so the
                // materialized carrier is the absorption itself.
                _resultMaterializer.Materialize(e);
                return;
            }

            if (_resultMaterializer is null
                && _resultAdapter is not null
                && _resultAdapter.TryGetException(in Unit.Value, out var fastEx) && fastEx is not null)
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

                // The value channel; see the result-producing strategy.
                if (_resultAdapter is not null && _resultAdapter.TryGetException(in Unit.Value, out var carried) && carried is not null)
                {
                    exception = carried;
                }

                if (exception is null && postInterceptorCount > 0)
                {
                    var (postResult, postCarried) = await PostInterceptorInvocationStrategy<TMessage, Unit>.Invoke(
                        messageDependencies, _resultAdapter, serviceProvider, message, result, context);

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
                if (exceptionInterceptorCount == 0 && _resultAdapter is null)
                {
                    exception = e;
                    throw;
                }

                exception = e;

                if (_resultMaterializer is not null)
                {
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
                else if (_resultMaterializer is null)
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
    /// result-producing strategy for the dispatch rules.
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