
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;



/// <summary>
/// Executes pre-interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// This strategy runs both direct and indirect pre-interceptors in the order:
/// 1. Direct pre-interceptors
/// 2. Indirect pre-interceptors
/// 
/// Events are raised before and after each interceptor, as well as at the beginning and end of the entire pipeline.
/// This ensures monitoring and potential logging of each interceptor execution.
/// </remarks>
internal static class TaskPreInterceptorInvocationStrategy
{
    /// <summary>
    /// Invokes a collection of pre-interceptors in sequence.
    /// </summary>
    private static async Task<object> InvokeCollection(
        ILazyHandlerCollection<IPreInterceptor,IPreInterceptorDescriptor> interceptors,  object message, IExecutionContext context)
    {
       
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            var result = handler.Handle(message, context);
            var awaitedResult = await TaskInvocationHelper.AwaitResult(result);
            if (awaitedResult != null)
            {
                message = awaitedResult;
            }
        }
        return message;
    }
    
    /// <summary>
    /// Executes all pre-interceptors (direct and indirect) for the specified message.
    /// </summary>
    public static async Task<object> Invoke(IMessageDependencies messageDependencies, object message, IExecutionContext executionContext)
    {
        if (messageDependencies.PreInterceptors.Count > 0)
            message = await InvokeCollection(messageDependencies.PreInterceptors ,message, executionContext);

        if (messageDependencies.IndirectPreInterceptors.Count > 0)
            message = await InvokeCollection(messageDependencies.IndirectPreInterceptors ,message, executionContext);

        return message;
    }
}

