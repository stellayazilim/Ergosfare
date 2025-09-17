
using System.Threading.Tasks;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Invokers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;



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
internal sealed class TaskPreInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService) : PreInvoker(messageDependencies, resultAdapterService)

{
    /// <summary>
    /// Invokes a collection of pre-interceptors in sequence.
    /// </summary>
    /// <param name="interceptors">The collection of pre-interceptors to execute.</param>
    /// <param name="message">The message being processed, which may be transformed by interceptors.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed message after all interceptors in the collection have executed.
    /// </returns>
    private async Task<object> InvokePostInterceptorCollection(
        ILazyHandlerCollection<IPreInterceptor,IPreInterceptorDescriptor> interceptors,  object message, IExecutionContext executionContext)
    {
       
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            BeginPreInterceptorInvocationSignal.Invoke(message, null, handler.GetType());
            message = await (Task<object>)handler.Handle(message,  executionContext);
            FinishPreInterceptorInvocationSignal.Invoke(message, null);
        }
        return message;
    }
    
    /// <summary>
    /// Executes all pre-interceptors (direct and indirect) for the specified message.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed message after all pre-interceptors have executed.
    /// </returns>
    public override async Task<object> Invoke(object message, IExecutionContext executionContext)
    {
        BeginPreInterceptingSignal.Invoke(message, executionContext, PreInterceptorCount);
        message = await InvokePostInterceptorCollection(MessageDependencies.PreInterceptors ,message, executionContext);
        message = await InvokePostInterceptorCollection(MessageDependencies.IndirectPreInterceptors ,message, executionContext);
        FinishPreInterceptingSignal.Invoke(message, executionContext);
        return message;
    }
}

