using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes exception interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// This strategy runs both direct and indirect exception interceptors in the order:
/// 1. Direct exception interceptors
/// 2. Indirect exception interceptors
/// 
/// Events are raised before and after each interceptor, and at the beginning and end of the pipeline.
/// If no exception interceptors are registered, the captured exception is rethrown immediately.
/// </remarks>
internal static class TaskExceptionInterceptorInvocationStrategy
{
    private static async Task<object?> InvokeCollection(
        ILazyHandlerCollection<IExceptionInterceptor,IExceptionInterceptorDescriptor> interceptors,  
        object message, object? result, ExceptionDispatchInfo dispatchInfo, IExecutionContext executionContext)
    {
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            var handleResult = handler.Handle(message, result, dispatchInfo.SourceException, executionContext);
            var awaitedResult = await TaskInvocationHelper.AwaitResult(handleResult);
            result = awaitedResult ?? result;
        }
        return result;
    }
    
    public static async Task<object?> Invoke(IMessageDependencies messageDependencies, object message, object? result, ExceptionDispatchInfo exceptionDispatchInfo,
        IExecutionContext executionContext)
    {
        if (messageDependencies.ExceptionInterceptors.Count == 0 && messageDependencies.IndirectExceptionInterceptors.Count == 0)
            exceptionDispatchInfo.Throw();
        
        result = await InvokeCollection(
            messageDependencies.ExceptionInterceptors, message, result, exceptionDispatchInfo, executionContext);
        result = await InvokeCollection(
            messageDependencies.IndirectExceptionInterceptors, message, result, exceptionDispatchInfo, executionContext);
        
        return result;
    }
}