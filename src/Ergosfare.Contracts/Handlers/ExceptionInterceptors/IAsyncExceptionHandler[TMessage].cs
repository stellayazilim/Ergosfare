using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IAsyncExceptionInterceptor<in TMessage>: IExceptionInterceptor<TMessage, object>
    where TMessage : notnull
{
    
    object IExceptionInterceptor<TMessage, object>
        .Handle(
            TMessage message, 
            object? messageResult, 
            Exception exception, 
            IExecutionContext context)
    {
        return HandleAsync(
            message, 
            messageResult, 
            exception, context, AmbientExecutionContext.Current.CancellationToken);
    }
    
    Task HandleAsync(
        TMessage message, 
        object? messageResult, 
        Exception exception, 
        IExecutionContext context, 
        CancellationToken cancellationToken = default);
}