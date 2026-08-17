using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Runs a void message through the single handler that serves it and the interceptor stages
/// around it.
/// </summary>
/// <typeparam name="TMessage">The message type being dispatched.</typeparam>
/// <remarks>
/// This is where the semantics of a single-handler void dispatch live: which handler wins,
/// how a pipeline with no interceptors is short-circuited, how a failure carried in a
/// result is treated, and what an abort skips. It is a static body the dispatches call with
/// state they resolved beforehand — nothing is decided per dispatch here.
/// </remarks>
internal static class VoidPipelineBody<TMessage> where TMessage : IMessage
{
    /// <summary>
    /// Runs the pipeline for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="messageDependencies">The message's participants, per stage.</param>
    /// <param name="resultAdapter">
    /// The result type's adapter, used to spot a failure carried in the result slot;
    /// <c>null</c> when there is none.
    /// </param>
    /// <param name="resultMaterializer">
    /// The result type's materializer. When present, a thrown failure is absorbed instead of
    /// reaching the caller; <c>null</c> keeps the default of throwing.
    /// </param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="messageDependencies"/> is <c>null</c>.</exception>
    /// <exception cref="MultipleHandlerFoundException">
    /// The level of handlers that serves the message holds more than one.
    /// </exception>
    /// <exception cref="NoHandlerFoundException">No handler is registered for the message.</exception>
    /// <exception cref="ExecutionAbortedException">A participant stopped the pipeline.</exception>
    internal static async ValueTask Run(
        TMessage message,
        IMessageDependencies messageDependencies,
        IResultAdapter<Unit>? resultAdapter,
        IResultMaterializer<Unit>? resultMaterializer,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(messageDependencies);

        // Handlers registered for the message type itself decide the dispatch on their own:
        // one of them serves the message however many covariant candidates exist, since a
        // covariant handler is a fallback rather than a rival. Only with no direct handler
        // does the covariant level come into play. Two candidates at the same level is a
        // contest, counted before anything is resolved so that neither claimant runs.
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

        // With no interceptors at all there is nobody to observe or change anything, so the
        // handler is called on its own. A failure then propagates untouched, which is what
        // the full pipeline does in this case too.
        if ((preInterceptorCount | postInterceptorCount | exceptionInterceptorCount | finalInterceptorCount) == 0)
        {
            var fastHandler = soleHandler.Resolve(serviceProvider);

            try
            {
                // No abort arm: a handler that stops its own dispatch has no stage left to
                // skip and nothing left to report, so the signal goes straight to the caller.
                await InvokeHandler(fastHandler, message, context);
            }
            catch (Exception e) when (resultMaterializer is not null && e is not ExecutionAbortedException)
            {
                // A void pipeline has no result to hand back, so building the failed carrier
                // is itself how the failure is absorbed.
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

        // The handler's task says when it finished and is awaited here; what travels through
        // the stages is the result slot, and a void pipeline has one value for it — Unit
        // once the handler has run, null before that.
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

                // A failure carried in the result reaches the exception stage below without
                // anything being thrown.
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
                // With no exception interceptors and no adapter, the failure keeps its
                // original path out of the pipeline.
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
                    // Nobody accepted the failure and nothing absorbs it, so it is thrown
                    // after the final stage has run.
                    unhandledException ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the pipeline. Nothing else runs — neither the exception
            // stage nor the final stage below — and the signal continues to the caller.
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
    /// Calls the handler through whichever main-handler contract it implements.
    /// </summary>
    /// <param name="handler">The resolved handler.</param>
    /// <param name="message">The message to hand it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    /// <exception cref="NotSupportedException">
    /// The handler implements no main-handler contract accepting
    /// <typeparamref name="TMessage"/>, which happens when a message is dispatched through a
    /// static type that erases its own.
    /// </exception>
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
