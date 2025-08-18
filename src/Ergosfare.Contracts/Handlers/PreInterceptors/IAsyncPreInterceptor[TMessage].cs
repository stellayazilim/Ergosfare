using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IAsyncPreInterceptor<in TMessage>:
    IPreInterceptor<TMessage> 
    where TMessage : notnull
{
    object IPreInterceptor<TMessage>.Handle(
        TMessage message,  IExecutionContext context) => HandleAsync(
            message, 
            context, 
            AmbientExecutionContext.Current.CancellationToken);
    
    Task HandleAsync(
        TMessage message, 
        IExecutionContext context, 
        CancellationToken cancellationToken = default);
}