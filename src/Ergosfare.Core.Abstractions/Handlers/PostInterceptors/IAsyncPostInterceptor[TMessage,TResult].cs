using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IAsyncPostInterceptor<in TMessage, in TResult>
    :IPostInterceptor<TMessage, TResult>
    where TMessage : notnull 
    where TResult: notnull
{
    
    object IPostInterceptor<TMessage, TResult>.Handle(
        TMessage message, 
        TResult? messageResult, 
        IExecutionContext context)
    {
        return HandleAsync(
            message, 
            messageResult, 
            AmbientExecutionContext.Current);
    }
    
    Task HandleAsync(TMessage message, TResult? messageResult, IExecutionContext context);
}