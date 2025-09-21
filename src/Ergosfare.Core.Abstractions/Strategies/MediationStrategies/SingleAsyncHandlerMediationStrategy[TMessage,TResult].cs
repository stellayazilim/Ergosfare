using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Ergosfare.Core.Abstractions.Strategies;

/// <summary>
/// Implements a mediation strategy for a single asynchronous handler.
/// Ensures that only one handler is executed for the message, invokes pre- and post-interceptors,
/// handles exceptions, and applies final interceptors. Supports optional result adaptation.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the handler.</typeparam>
public sealed class SingleAsyncHandlerMediationStrategy<TMessage, TResult>(IResultAdapterService? resultAdapterService) : IMessageMediationStrategy<TMessage, Task<TResult>> 
    where TMessage : notnull
{
    
    /// <summary>
    /// Mediates the message by invoking the handler along with pre-, post-, exception-, and final interceptors.
    /// </summary>
    /// <param name="message">The message to be handled.</param>
    /// <param name="messageDependencies">The dependencies of the message, including the registered handlers and interceptors.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation, returning the result of type <typeparamref name="TResult"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageDependencies"/> is null.</exception>
    /// <exception cref="MultipleHandlerFoundException">Thrown if more than one handler is registered for the message.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the handler is null or not of the expected type, or if a result is missing after an aborted execution.</exception>
    public async Task<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }
        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }
            
        TResult result = default!;
        Exception? exception = null;
        try
        {
            var preInvoker = new TaskPreInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            
            message = (TMessage) await preInvoker.Invoke(message, context);

            var handler = messageDependencies.Handlers.Single().Handler.Value;

            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"Handler for {typeof(TMessage).Name} is not of the expected type.");
            }

            result = await (Task<TResult>)handler.Handle(message, context);

            if (resultAdapterService is not null)
            {
                var ex = resultAdapterService.LookupException(result);
                if (ex is not null) throw ex;
            }

            var postInvoker = new TaskPostInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            
            var postResult = (TResult?)await postInvoker.Invoke(message, result, context);
            result = postResult is null ? result : postResult;
        }
        catch (ExecutionAbortedException) 
        {
            if (context.Result is null)
            {
                throw new InvalidOperationException(
                    $"A Message result of type '{typeof(TResult).Name}' is required when the execution is aborted as this message has a specific result.");
            }

            return await Task.FromResult((TResult)context.Result);

        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            exception = e;
            
            var exceptionInvoker = new TaskExceptionInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            var exceptionResult  = (TResult?)await exceptionInvoker.Invoke(
                message, 
                result, 
                ExceptionDispatchInfo.Capture(exception),
                context);
            
            result = exceptionResult is null ? result : exceptionResult;
            
        }
        finally
        {
            var finalInvoker = new TaskFinalInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            await finalInvoker.Invoke(message, result, exception, context);
        }
        
        return result;
    }
}