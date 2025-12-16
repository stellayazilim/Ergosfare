
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Invokers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes final interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// Final interceptors are executed after pre-, post-, and exception interceptors.
/// Both direct and indirect final interceptors are run in order:
/// 1. Direct final interceptors
/// 2. Indirect final interceptors
/// 
/// Signals are raised before and after each interceptor, and at the beginning and end of the pipeline.
/// Final interceptors do not rethrow exceptions; they are intended for cleanup, logging, or finalization logic.
/// </remarks>
internal sealed class TaskFinalInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService) : FinalInvoker(messageDependencies, resultAdapterService)
{

    /// <summary>
    /// Invokes a collection of final interceptors in sequence.
    /// </summary>
    /// <param name="interceptors">The collection of final interceptors to execute.</param>
    /// <param name="message">The message being processed, which is provided to each interceptor.</param>
    /// <param name="result">The current result of the message handling, which may be passed to interceptors.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task InvokeFinalInterceptorCollection(
        ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> interceptors,
        object message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        foreach (var interceptor in interceptors)
        {
            // resolve handler lazily
            var handler = interceptor.Handler.Value;
            
            // Execute interceptor
            await (Task) handler.Handle(message, result, exception, executionContext);
        }
    }
    
    
    /// <summary>
    /// Executes all final interceptors (direct and indirect) for the specified message.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be passed to final interceptors.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation of executing all final interceptors.</returns>
    public override async Task Invoke(object message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        
        // Run direct final interceptors
        await InvokeFinalInterceptorCollection(
            MessageDependencies.FinalInterceptors, message, result, exception, executionContext);
        
        // Run indirect final interceptors
        await InvokeFinalInterceptorCollection(
            MessageDependencies.IndirectFinalInterceptors, message, result, exception, executionContext);
        
    }
}