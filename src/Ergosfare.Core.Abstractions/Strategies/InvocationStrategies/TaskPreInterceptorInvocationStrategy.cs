using System.Linq;
using System.Threading.Tasks;
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
    /// Invokes a collection of pre-interceptors in sequence, respecting checkpoint state.
    /// Only interceptors that have not yet successfully executed are invoked; previously 
    /// completed interceptors will reuse their snapshotted message.
    /// </summary>
    /// <param name="interceptors">
    /// The collection of pre-interceptors to execute. Each interceptor may transform the message.
    /// </param>
    /// <param name="message">
    /// The message being processed, which may be modified by the interceptors. The final 
    /// returned message reflects all transformations applied by the executed interceptors.
    /// </param>
    /// <param name="context">
    /// The current execution context containing checkpoints, message state, and other pipeline metadata.
    /// </param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation. The result contains 
    /// the transformed message after executing all applicable interceptors, skipping any that have 
    /// already succeeded according to their checkpoint.
    /// </returns>
    private async Task<object> InvokePostInterceptorCollection(
        ILazyHandlerCollection<IPreInterceptor,IPreInterceptorDescriptor> interceptors,  object message, IExecutionContext context)
    {
       
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            
            // Try to find an existing checkpoint for this interceptor
            var checkpoint = context.Checkpoints?.FirstOrDefault(x => x.HandlerType == handler.GetType());


            if (checkpoint is null)
            {
                checkpoint = new PipelineCheckpoint(
                    handler.GetType().Name,   // checkpoint ID
                    message,                  // input message
                    null,                     // result placeholder
                    handler.GetType(),        // handler type
                    null,                     // parent checkpoint
                    []                        // sub-checkpoints
                );
                context.Checkpoints?.Add(checkpoint);
            }
            if (!checkpoint.Success)
            {
                // Signal: beginning of pre-interceptor execution
                BeginPreInterceptorInvocationSignal.Invoke(message, null, handler.GetType());

                // Execute interceptor handler and await result
                message = await (Task<object>)handler.Handle(message, context);

                // Mark checkpoint as successful
                ((PipelineCheckpoint)checkpoint).Success = true;
                ((PipelineCheckpoint)checkpoint).Message = message;

                // Update the context message to the latest result
                context.Message = message;

                // Signal: end of pre-interceptor execution
                FinishPreInterceptorInvocationSignal.Invoke(message, null);
            }
            // Step already succeeded → reuse checkpointed message
            else message = checkpoint.Message;
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

