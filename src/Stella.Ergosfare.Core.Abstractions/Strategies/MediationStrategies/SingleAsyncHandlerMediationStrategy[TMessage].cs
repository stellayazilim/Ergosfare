using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
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
    IResultAdapterService? resultAdapterService) : IMessageMediationStrategy<TMessage, Task> where TMessage : IMessage
{
    /// <summary>
    ///     Mediates a message by executing the appropriate handler and orchestrating the handling pipeline.
    /// </summary>
    /// <param name="message">The message to be mediated.</param>
    /// <param name="messageDependencies">
    ///     The dependencies required for message handling, including handlers, pre-handlers,
    ///     post-handlers, and error handlers.
    /// </param>
    /// <param name="context">
    ///     The context in which the mediation is executed, providing access to cancellation tokens,
    ///     shared data, and other execution-related information.
    /// </param>
    /// <returns>A task representing the asynchronous mediation operation.</returns>
    /// <exception cref="MultipleHandlerFoundException">Thrown when more than one handler is found for the message type.</exception>
    /// <remarks>
    ///     The mediation process includes executing pre-handlers, the main handler, and post-handlers in sequence.
    ///     If an exception occurs during any stage, the appropriate error handlers are executed.
    ///     If a <see cref="ExecutionAbortedException" /> is caught, the mediation process is aborted without error.
    /// </remarks>
    public Task Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }

        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }

        // Fast path: No interceptors
        if (!messageDependencies.HasInterceptors)
        {
            if (messageDependencies.Handlers is SingleLazyHandlerCollection<IHandler, IMainHandlerDescriptor> single)
            {
                var h = single.SingleHandler.Handler;
                if (h is IHandler<TMessage, Task> stronglyTyped)
                {
                    var t = stronglyTyped.Handle(message, context);
                    if (resultAdapterService == null) return t;
                    return MediateWithResultAdapterOnly(t);
                }

                var task = (Task)h.Handle(message, context);
                if (resultAdapterService == null) return task;
                return MediateWithResultAdapterOnly(task);
            }

            var handler = messageDependencies.Handlers.Single().Handler;
            if (handler is null)
            {
                throw new InvalidOperationException($"Handler for {typeof(TMessage).Name} is not of the expected type.");
            }

            if (handler is IHandler<TMessage, Task> stronglyTypedHandler)
            {
                var t = stronglyTypedHandler.Handle(message, context);
                if (resultAdapterService == null) return t;
                return MediateWithResultAdapterOnly(t);
            }

            var taskResult = (Task)handler.Handle(message, context);
            if (resultAdapterService == null) return taskResult;

            return MediateWithResultAdapterOnly(taskResult);
        }

        return MediateFull(message, messageDependencies, context);
    }

    private async Task MediateWithResultAdapterOnly(Task task)
    {
        await task;
        var ex = resultAdapterService?.LookupException(task);
        if (ex is not null) throw ex;
    }

    private async Task MediateFull(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context)
    {
        Task? result = null;
        Exception? exception = null;
        try
        {
            message = (TMessage) await TaskPreInterceptorInvocationStrategy.Invoke(messageDependencies, message, context);

            var handler = messageDependencies.Handlers.Single().Handler;
            if (handler is null)
            {
                throw new InvalidOperationException($"Handler for {typeof(TMessage).Name} is not of the expected type.");
            }
            
            result =  (Task)handler.Handle(message, context);
            await result;

            var ex = resultAdapterService?.LookupException(result);
            if (ex != null) throw ex;

            var invokedPostResult =  (Task?) await TaskPostInterceptorInvocationStrategy.Invoke(messageDependencies, resultAdapterService, message, result, context);
            result = invokedPostResult ?? result;
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            exception = e;
            var invokedResult = (Task?) await TaskExceptionInterceptorInvocationStrategy.Invoke(messageDependencies, message, result, ExceptionDispatchInfo.Capture(e),
                context);
            result = invokedResult ?? result;
        }
        finally
        {
            await TaskFinalInterceptorInvocationStrategy.Invoke(messageDependencies, message, result, exception, context);
        }
    }
}