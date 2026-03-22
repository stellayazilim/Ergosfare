
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes post-interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// This strategy runs both direct and indirect post-interceptors in the order:
/// 1. Direct post-interceptors
/// 2. Indirect post-interceptors
/// 
/// Events are raised before and after each interceptor, as well as at the beginning and end of the pipeline,
/// allowing monitoring and potential logging of each interceptor execution.
/// </remarks>
internal static class TaskPostInterceptorInvocationStrategy
{
    private static async Task<object?> InvokeCollection(
        IResultAdapterService? resultAdapterService,
        ILazyHandlerCollection<IPostInterceptor,IPostInterceptorDescriptor> interceptors,  object message, object? result,  IExecutionContext context)
    {
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            var handleResult = handler.Handle(message, result, context);
            var awaitedResult = await TaskInvocationHelper.AwaitResult(handleResult);

            result = awaitedResult ?? result;

            var ex = resultAdapterService?.LookupException(result);
            if (ex != null) throw ex;
        }
        return result;
    }
    
    public static async Task<object?> Invoke(IMessageDependencies messageDependencies, IResultAdapterService? resultAdapterService, object message, object? result,  IExecutionContext context)
    {
        if (messageDependencies.PostInterceptors.Count > 0)
            result = await InvokeCollection(resultAdapterService, messageDependencies.PostInterceptors, message, result, context);

        if (messageDependencies.IndirectPostInterceptors.Count > 0)
            result = await InvokeCollection(resultAdapterService, messageDependencies.IndirectPostInterceptors, message, result, context);

        return result;
    }
}