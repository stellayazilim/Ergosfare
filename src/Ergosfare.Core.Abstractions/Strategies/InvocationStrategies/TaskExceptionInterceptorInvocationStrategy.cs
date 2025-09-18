using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Invokers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


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
internal sealed class TaskExceptionInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService) : ExceptionInvoker(messageDependencies, resultAdapterService)
{


    
    /// <summary>
    /// Invokes a collection of exception interceptors in sequence.
    /// </summary>
    /// <param name="interceptors">The collection of exception interceptors to execute.</param>
    /// <param name="message">The message being processed, which is provided to each interceptor.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by interceptors.</param>
    /// <param name="dispatchInfo">The captured exception information for the current pipeline invocation.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all interceptors in the collection have executed.
    /// </returns>
    private async Task<object?> InvokeExceptionInterceptorCollection(
        ILazyHandlerCollection<IExceptionInterceptor,IExceptionInterceptorDescriptor> interceptors,  
        object message, object? result, ExceptionDispatchInfo dispatchInfo, IExecutionContext executionContext)
    {
       
        foreach (var interceptor in interceptors)
        {
            var handler = interceptor.Handler.Value;
            BeginExceptionInterceptorInvocationSignal.Invoke(message, result, handler.GetType(), dispatchInfo.SourceException);
            var objectResult = handler.Handle(message, result, dispatchInfo.SourceException, executionContext);
            result = await ConvertTask(objectResult);
            FinishPreInterceptorInvocationSignal.Invoke(message, result);
        }
        return await Task.FromResult(result);
    }
    
    
    /// <summary>
    /// Executes all exception interceptors (direct and indirect) for the specified message and result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by exception interceptors.</param>
    /// <param name="exceptionDispatchInfo">The captured exception information to be passed to interceptors.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all exception interceptors have executed.
    /// If no interceptors exist, the captured exception is rethrown.
    /// </returns>
    public override async Task<object?> Invoke(object message, object? result, ExceptionDispatchInfo exceptionDispatchInfo,
        IExecutionContext executionContext)
    {
        if (ExceptionInterceptorCount == 0) exceptionDispatchInfo.Throw();
        
        BeginExceptionInterceptingSignal.Invoke(message, result, exceptionDispatchInfo.SourceException, ExceptionInterceptorCount);
        result = await InvokeExceptionInterceptorCollection(
            MessageDependencies.ExceptionInterceptors, message, result, exceptionDispatchInfo, executionContext);
        result = await InvokeExceptionInterceptorCollection(
            MessageDependencies.IndirectExceptionInterceptors, message, result, exceptionDispatchInfo, executionContext);
        FinishExceptionInterceptingSignal.Invoke(message, result, exceptionDispatchInfo.SourceException);
        
        return result;
    }
}