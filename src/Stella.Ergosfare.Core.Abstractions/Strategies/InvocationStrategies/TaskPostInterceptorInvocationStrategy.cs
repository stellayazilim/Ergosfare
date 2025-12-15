using System.Linq;
using System.Threading.Tasks;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Invokers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

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
internal sealed class TaskPostInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService) : 
    PostInvoker(messageDependencies, resultAdapterService)
{

    /// <summary>
    /// Invokes a collection of post-interceptors in sequence, respecting checkpoint state.
    /// Only interceptors that have not yet successfully executed are invoked; previously 
    /// completed interceptors will reuse their checkpointed result.
    /// </summary>
    /// <param name="interceptors">
    /// The collection of post-interceptors to execute. Each interceptor may transform the result.
    /// </param>
    /// <param name="message">
    /// The message being processed, which may be used by each interceptor.
    /// </param>
    /// <param name="result">
    /// The current result of message handling. The final returned result reflects all transformations
    /// applied by the executed interceptors.
    /// </param>
    /// <param name="context">
    /// The execution context containing checkpoints, message state, and other pipeline metadata.
    /// </param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation. The result contains 
    /// the transformed result after executing all applicable post-interceptors, skipping any that 
    /// have already succeeded according to their checkpoint.
    /// </returns>
    private async Task<object?> InvokePostInterceptorCollection(
        ILazyHandlerCollection<IPostInterceptor,IPostInterceptorDescriptor> interceptors,  object message, object? result,  IExecutionContext context)
    {
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
     


            // Signal: beginning of pre-interceptor execution
            BeginPreInterceptorInvocationSignal.Invoke(message, null, handler.GetType());
                
            // Execute interceptor handler and await result
            result = await (Task<object?>)handler.Handle(message, result, context);

            var ex = resultAdapterService?.LookupException(result);
            
            if (ex != null) throw ex;
            
            // Signal: end of pre-interceptor execution
            FinishPreInterceptorInvocationSignal.Invoke(message, null);
  
        }
        return result;
    }
    
    
    /// <summary>
    /// Executes all post-interceptors (direct and indirect) for the specified message and result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by post-interceptors.</param>
    /// <param name="context">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all post-interceptors have executed.
    /// </returns>
    public override async Task<object?> Invoke(object message, object? result,  IExecutionContext context)
    {
        BeginPostInterceptingSignal.Invoke(message, result, PostInterceptorCount);
        result = await InvokePostInterceptorCollection(MessageDependencies.PostInterceptors,message, result, context);
        result = await InvokePostInterceptorCollection(MessageDependencies.IndirectPostInterceptors, message, result, context);
        FinishPostInterceptingSignal.Invoke(message, result);
        return result;
    }
}