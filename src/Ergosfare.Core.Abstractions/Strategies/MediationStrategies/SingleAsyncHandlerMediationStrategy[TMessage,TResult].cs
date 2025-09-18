using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Ergosfare.Core.Abstractions.Strategies;

/// <summary>
///     Represents a mediation strategy that processes a message through a single asynchronous handler.
/// </summary>
/// <typeparam name="TMessage">The type of message being mediated.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the handler.</typeparam>
/// <remarks>
///     This strategy ensures that only one handler is registered for the message type and then:
///     1. Executes pre-handlers.
///     2. Delegates the message processing to the registered handler.
///     3. Executes post-handlers.
///     In case of any exception during the process, it delegates the error handling to the registered error handlers.
/// </remarks>
public sealed class SingleAsyncHandlerMediationStrategy<TMessage, TResult>(IResultAdapterService? resultAdapterService) : IMessageMediationStrategy<TMessage, Task<TResult>> 
    where TMessage : notnull
{
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
            if (context.MessageResult is null)
            {
                throw new InvalidOperationException(
                    $"A Message result of type '{typeof(TResult).Name}' is required when the execution is aborted as this message has a specific result.");
            }

            return await Task.FromResult((TResult)context.MessageResult);

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