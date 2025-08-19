using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IAsyncExceptionInterceptor<in TMessage, in TResult>: 
    IExceptionInterceptor<TMessage, TResult> where TMessage : notnull
{

    object IExceptionInterceptor<TMessage, TResult>.Handle(
        TMessage message,
        TResult? result,
        Exception exception,
        IExecutionContext context)
    {
        return Handle(message, result, exception, context);
    }
       
        
    
    Task HandleAsync(TMessage message, TResult result, Exception exception, IExecutionContext context, CancellationToken cancellation = default);
}