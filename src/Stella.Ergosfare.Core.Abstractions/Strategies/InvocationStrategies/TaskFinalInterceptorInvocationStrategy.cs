
using Stella.Ergosfare.Core.Abstractions.Handlers;
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
internal static class TaskFinalInterceptorInvocationStrategy
{
    private static async Task InvokeCollection(
        ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> interceptors,
        object message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            var handleResult = handler.Handle(message, result, exception, executionContext);
            await TaskInvocationHelper.AwaitResult(handleResult);
        }
    }
    
    public static async Task Invoke(IMessageDependencies messageDependencies, object message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        if (messageDependencies.FinalInterceptors.Count > 0)
            await InvokeCollection(
                messageDependencies.FinalInterceptors, message, result, exception, executionContext);
        
        if (messageDependencies.IndirectFinalInterceptors.Count > 0)
            await InvokeCollection(
                messageDependencies.IndirectFinalInterceptors, message, result, exception, executionContext);
    }
}