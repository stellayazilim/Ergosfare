using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Ergosfare.Core.Abstractions.Strategies;

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
    public async Task Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context)
    {
        
        
        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }

        Task? result = null;
        Exception? exception = null;
        try
        {
            var preInvoker = new TaskPreInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            message = (TMessage) await preInvoker.Invoke(message, context);
            result = (Task)messageDependencies.Handlers.Single().Handler.Value.Handle(message, context);
            await result;
            
            var postInvoker = new TaskPostInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            var invokedPostResult =  (Task?) await postInvoker.Invoke(message, result, context);
            result = invokedPostResult ?? result;
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            exception = e;
            var exceptionInvoker = new TaskExceptionInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            var invokedResult = (Task?) await exceptionInvoker.Invoke(message, result, ExceptionDispatchInfo.Capture(e),
                context);
            result = invokedResult ?? result;

        }
        finally
        {
            var finalInvoker = new TaskFinalInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            await finalInvoker.Invoke(message, result, exception, context);
        }
    }
}