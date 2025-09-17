using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.Extensions;

public static class MessageDependencyExtensions
{
    
    
    /// <summary>
    /// Runs all pre-interceptors for the given <paramref name="message"/> in the order:
    /// indirect pre-interceptors first, then direct pre-interceptors.  
    /// Each interceptor can transform the message, and the transformed message is passed to the next interceptor.
    /// </summary>
    /// <param name="messageDependencies">
    /// The collection of message dependencies, containing both direct and indirect pre-interceptors.
    /// </param>
    /// <param name="message">
    /// The original message to be intercepted. This object may be transformed by the interceptors.
    /// </param>
    /// <param name="context">
    /// The current execution context, passed to each interceptor.
    /// </param>
    /// <returns>
    /// A <see cref="Task{Object}"/> that produces the final transformed message
    /// after all pre-interceptors have been applied.
    /// </returns>
    /// <remarks>
    /// During execution, the following pipeline events are invoked:
    /// <list type="bullet">
    /// <item><see cref="BeginPreInterceptingSignal"/> before any interceptors run.</item>
    /// <item><see cref="BeginPreInterceptorInvocationSignal"/> before each individual interceptor is invoked.</item>
    /// <item><see cref="FinishPreInterceptorInvocationSignal"/> after each individual interceptor has finished.</item>
    /// <item><see cref="FinishPreInterceptingSignal"/> after all pre-interceptors have completed.</item>
    /// </list>
    /// This allows subscribers to monitor the progress of the pre-interceptor pipeline.
    /// </remarks>
    public static async Task<object> RunAsyncPreInterceptors(this IMessageDependencies messageDependencies, object message, IExecutionContext context)
    {
        object input = message;
        var total = messageDependencies.IndirectPreInterceptors.Count + messageDependencies.PreInterceptors.Count;
        BeginPreInterceptingSignal.Invoke(input, null, (ushort)total);
        
        foreach (var preHandler in messageDependencies.IndirectPreInterceptors)
        {
            // pre interceptor begin invocation event
            BeginPreInterceptorInvocationSignal.Invoke(message, null, preHandler.Handler.Value.GetType());
            
            // pre interceptor invoked
            input =  await (preHandler.Handler.Value.Handle(input, context) as Task<object>)!;
            
            // finish pre interceptor invocation event
            FinishPreInterceptorInvocationSignal.Invoke(input, null);
        }
        
        foreach (var preHandler in messageDependencies.PreInterceptors)
        {
            // pre interceptor begin invocation event
            BeginPreInterceptorInvocationSignal.Invoke(input, null, preHandler.Handler.Value.GetType());
            input =  await (preHandler.Handler.Value.Handle(input, context) as Task<object>)!;
            // finish pre interceptor invocation event
            FinishPreInterceptorInvocationSignal.Invoke(input, null);
        }
        FinishPreInterceptingSignal.Invoke(input, null);
        return input;
    }
    
    
/// <summary>
/// Runs all post-interceptors for the given <paramref name="message"/> in the order:
/// direct post-interceptors first, then indirect post-interceptors.  
/// Each interceptor can transform the result, and the transformed result is passed to the next interceptor.
/// </summary>
/// <param name="messageDependencies">
/// The collection of message dependencies, containing both direct and indirect post-interceptors.
/// </param>
/// <param name="message">
/// The original message that was handled. This is provided to all interceptors.
/// </param>
/// <param name="messageResult">
/// The result produced by the main handler. This can be <c>null</c> if the handler did not produce a result.
/// </param>
/// <param name="context">
/// The current execution context, passed to each interceptor.
/// </param>
/// <param name="resultAdapterService">
/// Optional service used to detect exceptions embedded in the result object.  
/// If an exception is found in the result, it is thrown immediately and the corresponding
/// <see cref="FinishPostInterceptingWithExceptionSignal"/> is invoked.
/// </param>
/// <returns>
/// A <see cref="Task{Object}"/> that produces the final transformed result
/// after all post-interceptors have been applied.
/// </returns>
/// <remarks>
/// During execution, the following pipeline events are invoked:
/// <list type="bullet">
/// <item><see cref="BeginPostInterceptingSignal"/> before any post-interceptors run.</item>
/// <item><see cref="BeginPostInterceptorInvocationSignal"/> before each individual interceptor is invoked.</item>
/// <item><see cref="FinishPostInterceptorInvocationSignal"/> after each individual interceptor has finished.</item>
/// <item><see cref="FinishPostInterceptingWithExceptionSignal"/> if an exception is detected in the result by a <paramref name="resultAdapterService"/>.</item>
/// <item><see cref="FinishPostInterceptingSignal"/> after all post-interceptors have completed.</item>
/// </list>
/// This allows subscribers to monitor the progress of the post-interceptor pipeline and handle exceptions
/// that are embedded in results without requiring the original handler to throw.
/// </remarks>
    public static async Task<object> RunAsyncPostInterceptors(
        this IMessageDependencies messageDependencies,
        object message,
        object? messageResult,
        IExecutionContext context,
        IResultAdapterService? resultAdapterService)
    {
        // output can be nullable if result doesnt produced
        var output = messageResult;
        // total amaunt of post-interceptor
        var total = messageDependencies.PostInterceptors.Count + messageDependencies.IndirectPostInterceptors.Count;
        
        BeginPostInterceptingSignal.Invoke(message, messageResult, (ushort)total);
        
        foreach (var postInterceptor in messageDependencies.PostInterceptors)
        {
            BeginPostInterceptorInvocationSignal.Invoke(message, output, postInterceptor.Handler.Value.GetType());
            output = await (postInterceptor.Handler.Value
                .Handle(message, output, context) as Task<object>)!;
            FinishPostInterceptorInvocationSignal.Invoke(message, output);
            // check if exception atteched to output
            var ex = resultAdapterService?.LookupException(output);
            if (ex != null)
            {
                FinishPostInterceptingWithExceptionSignal.Invoke(message, output, postInterceptor.Handler.Value.GetType(), ex);
                throw ex;
            }
        }

        foreach (var postInterceptor in messageDependencies.IndirectPostInterceptors)
        {
         
            
            BeginPostInterceptorInvocationSignal.Invoke(message, output, postInterceptor.Handler.Value.GetType());
            output = await (postInterceptor.Handler.Value
                .Handle(message, output, context) as Task<object>)!;
            FinishPostInterceptorInvocationSignal.Invoke(message, output);
            if (resultAdapterService is not null)
            {
                var ex = resultAdapterService.LookupException(output);
                if (ex is not null)
                {
                    FinishPostInterceptingWithExceptionSignal.Invoke(message, output, postInterceptor.Handler.Value.GetType(), ex);
                    throw ex;
                }
            }
        }
        FinishPostInterceptingSignal.Invoke(message, output);
        return output;
    }
    
    
/// <summary>
/// Runs all exception interceptors for the given <paramref name="message"/> in the order:
/// direct exception interceptors first, then indirect exception interceptors.  
/// Each interceptor can transform the result or handle the exception.  
/// If no interceptors are registered, the captured exception is rethrown.
/// </summary>
/// <param name="messageDependencies">
/// The collection of message dependencies, containing both direct and indirect exception interceptors.
/// </param>
/// <param name="message">
/// The message that triggered the exception. This is provided to all interceptors.
/// </param>
/// <param name="messageResult">
/// The result produced by the main handler before the exception occurred. This may be <c>null</c>.
/// </param>
/// <param name="exceptionDispatchInfo">
/// The captured exception information. If no interceptors exist, this exception is rethrown.
/// </param>
/// <param name="context">
/// The current execution context, passed to each interceptor.
/// </param>
/// <returns>
/// A <see cref="Task{Object}"/> that produces the transformed result after all exception interceptors have run.
/// This may be the same as <paramref name="messageResult"/> or a modified result from one of the interceptors.
/// </returns>
/// <remarks>
/// During execution, the following pipeline events are invoked:
/// <list type="bullet">
/// <item><see cref="BeginExceptionInterceptingEvent"/> before any exception interceptors run.</item>
/// <item><see cref="BeginExceptionInterceptorInvocationSignal"/> before each individual interceptor is invoked.</item>
/// <item><see cref="FinishExceptionInterceptorInvocationSignal"/> after each individual interceptor has finished.</item>
/// <item><see cref="FinishExceptionInterceptingSignal"/> after all exception interceptors have completed.</item>
/// </list>
/// This allows subscribers to monitor the exception handling pipeline and transform the result without throwing
/// the original exception immediately. If no interceptors exist, the captured exception is rethrown.
/// </remarks>
    public static async Task<object?> RunAsyncExceptionInterceptors(
        this IMessageDependencies messageDependencies,
        object message,
        object? messageResult,
        ExceptionDispatchInfo exceptionDispatchInfo,
        IExecutionContext context)
    {
        var total = messageDependencies.ExceptionInterceptors.Count + messageDependencies.IndirectExceptionInterceptors.Count;
        // if no exception interceptors exist, rethrow
        if (total == 0)
            exceptionDispatchInfo.Throw();
        
        BeginExceptionInterceptingSignal.Invoke(message, messageResult, exceptionDispatchInfo.SourceException, (ushort)total);
        var output = messageResult;
        // run direct exception interceptors
        foreach (var errorHandler in messageDependencies.ExceptionInterceptors)
        {
            BeginExceptionInterceptorInvocationSignal.Invoke(message, output, errorHandler.Handler.Value.GetType(), exceptionDispatchInfo.SourceException);
            var task = errorHandler.Handler.Value.Handle(
                message, 
                output, 
                exceptionDispatchInfo.SourceException, 
                context) as  Task<object>;

            if (task != null) output = await task;
            FinishExceptionInterceptorInvocationSignal.Invoke(message, output,  exceptionDispatchInfo.SourceException);
        }
        // run indirect exception interceptors
        foreach (var errorHandler in messageDependencies.IndirectExceptionInterceptors)
        {
            
            BeginExceptionInterceptorInvocationSignal.Invoke(message, output, errorHandler.Handler.Value.GetType(), exceptionDispatchInfo.SourceException);
            var task = errorHandler.Handler.Value.Handle(message, output, exceptionDispatchInfo.SourceException, context) as  Task<object>;
            
            if (task != null) output = await task;
            FinishExceptionInterceptorInvocationSignal.Invoke(message, output,  exceptionDispatchInfo.SourceException);
        }
        FinishExceptionInterceptingSignal.Invoke(message, output,  exceptionDispatchInfo.SourceException);
        return output;
    }

    /// <summary>
    /// Executes all registered final interceptors for a given message in order, including both direct and indirect interceptors.
    /// Final interceptors run after pre-, post-, and exception interceptors, and can perform cleanup, logging, or other finalization logic.
    /// </summary>
    /// <param name="messageDependencies">The message dependencies containing the final interceptors to execute.</param>
    /// <param name="message">The message being processed.</param>
    /// <param name="messageResult">The current result of the message, if any.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="context">The execution context for this message pipeline.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation of running all final interceptors.</returns>
    public static async Task RunAsyncFinalInterceptors(
        this IMessageDependencies messageDependencies,
        object message,
        object? messageResult,
        Exception? exception,
        IExecutionContext context)
    {
        // Compute the total number of final interceptors
        var total =  messageDependencies.FinalInterceptors.Count + messageDependencies.IndirectFinalInterceptors.Count;
        // Signal the beginning of the final intercepting stage
        BeginFinalInterceptingSignal.Invoke(message, messageResult, exception, (ushort)total);
        // Execute all direct final interceptors
        foreach (var finalHandler in messageDependencies.FinalInterceptors)
        {
            // Signal the start of an individual final interceptor
            BeginFinalInterceptorInvocationSignal.Invoke(message, messageResult, exception, finalHandler.Handler.Value.GetType());
            // Invoke the interceptor
            await (Task) finalHandler.Handler.Value.Handle(message, messageResult, exception, context);
            // Signal completion of this interceptor
            FinishFinalInterceptorInvocationSignal.Invoke(message, messageResult);
        }
        

        // Execute all indirect final interceptors
        foreach (var finalHandler in messageDependencies.IndirectFinalInterceptors)
        {
            // Signal the start of an individual final interceptor
            BeginFinalInterceptorInvocationSignal.Invoke(message, messageResult, exception, finalHandler.Handler.Value.GetType());
            // Invoke the interceptor
            await (Task) finalHandler.Handler.Value.Handle(message, messageResult, exception,  context);
            // Signal completion of this interceptor
            FinishFinalInterceptorInvocationSignal.Invoke(message, messageResult);

        }
        // Signal completion of the entire final interception stage
        FinishFinalInterceptingSignal.Invoke(message, messageResult);
    }
}