using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
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