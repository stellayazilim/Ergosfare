using System.Threading.Tasks;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Invokers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


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
internal sealed class TaskPostInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService) : 
    PostInvoker(messageDependencies, resultAdapterService)
{

    /// <summary>
    /// Invokes a collection of post-interceptors in sequence.
    /// </summary>
    /// <param name="interceptors">The collection of post-interceptors to execute.</param>
    /// <param name="message">The message being processed, which is provided to each interceptor.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by interceptors.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all interceptors in the collection have executed.
    /// </returns>
    private async Task<object?> InvokePostInterceptorCollection(
        ILazyHandlerCollection<IPostInterceptor,IPostInterceptorDescriptor> interceptors,  object message, object? result,  IExecutionContext executionContext)
    {
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            BeginPostInterceptorInvocationSignal.Invoke(message, result, handler.GetType());
            result = await (Task<object?>)handler.Handle(message, result, executionContext);
            FinishPostInterceptorInvocationSignal.Invoke(message, result);
        }
        return result;
    }
    
    
    /// <summary>
    /// Executes all post-interceptors (direct and indirect) for the specified message and result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by post-interceptors.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all post-interceptors have executed.
    /// </returns>
    public override async Task<object?> Invoke(object message, object? result, IExecutionContext executionContext)
    {
        BeginPostInterceptingSignal.Invoke(message, result, PostInterceptorCount);
        result = await InvokePostInterceptorCollection(MessageDependencies.PostInterceptors,message, result, executionContext);
        result = await InvokePostInterceptorCollection(MessageDependencies.IndirectPostInterceptors, message, result, executionContext);
        FinishPostInterceptingSignal.Invoke(message, result);
        return result;
    }
}