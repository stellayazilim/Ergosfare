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
public sealed class SingleAsyncHandlerMediationStrategy<TMessage>(
    IResultAdapterService? resultAdapterService) : IMessageMediationStrategy<TMessage, ValueTask> where TMessage : IMessage
{
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
    /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the message type.</exception>
    /// <remarks>
    ///     Pre-interceptors, the main handler and post-interceptors run in sequence; with no
    ///     interceptors registered the handler is invoked directly on a fast path. If an
    ///     exception occurs, the exception interceptors run; final interceptors always run.
    ///     An <see cref="ExecutionAbortedException" /> aborts the mediation without error.
    /// </remarks>
    public async ValueTask Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context, IServiceProvider serviceProvider)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }

        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }

        if (messageDependencies.Handlers.Count == 0)
        {
            throw new InvalidOperationException($"No handler is registered for {typeof(TMessage).Name}.");
        }

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
            var fastHandler = messageDependencies.Handlers[0].Resolve(serviceProvider);

            await InvokeHandler(fastHandler, message, context);

            var fastEx = resultAdapterService?.LookupException(CompletedResultBox.Instance);

            if (fastEx != null)
            {
                throw fastEx;
            }

            return;
        }

        // A ValueTask may be awaited only once, so after consuming the handler's ValueTask
        // the completed ValueTask.CompletedTask flows through the interceptor stages as the
        // (meaningless for void pipelines) result object — it is safely multi-awaitable.
        ValueTask? result = null;
        Exception? exception = null;
        try
        {
            if (preInterceptorCount > 0)
            {
                var preInvoker = new PreInterceptorInvocationStrategy<TMessage>(messageDependencies, serviceProvider);
                message = (TMessage) await preInvoker.Invoke(message, context);
            }

            var handler = messageDependencies.Handlers[0].Resolve(serviceProvider);

            await InvokeHandler(handler, message, context);
            result = ValueTask.CompletedTask;

            var ex = resultAdapterService?.LookupException(CompletedResultBox.Instance);

            if (ex != null)
            {
                throw ex;
            }

            if (postInterceptorCount > 0)
            {
                var postInvoker = new PostInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, resultAdapterService, serviceProvider);
                var invokedPostResult =  (ValueTask?) await postInvoker.Invoke(message, result, context);
                result = invokedPostResult ?? result;
            }
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            exception = e;

            if (exceptionInterceptorCount == 0)
            {
                throw;
            }

            var exceptionInvoker = new ExceptionInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, serviceProvider);
            var invokedResult = (ValueTask?) await exceptionInvoker.Invoke(message, result, ExceptionDispatchInfo.Capture(e),
                context);
            result = invokedResult ?? result;

        }
        finally
        {
            if (finalInterceptorCount > 0)
            {
                var finalInvoker = new FinalInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, serviceProvider);
                await finalInvoker.Invoke(message, result, exception, context);
            }
        }

    }

    /// <summary>
    /// Invokes the handler through its typed contract — no object-typed bridge; see the
    /// result-producing strategy for the dispatch rules.
    /// </summary>
    private static ValueTask InvokeHandler(object handler, TMessage message, IExecutionContext context)
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