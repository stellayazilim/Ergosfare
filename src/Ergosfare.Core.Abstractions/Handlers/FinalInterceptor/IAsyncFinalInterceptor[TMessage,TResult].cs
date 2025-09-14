using System;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;

public interface IAsyncFinalInterceptor<in TMessage, in TResult>: IFinalInterceptor<TMessage, TResult> 
    where TMessage : notnull
{

    object IFinalInterceptor<TMessage, TResult>.Handle(TMessage message, TResult? result, Exception? exception,
        IExecutionContext context)
    {
        return HandleAsync(message, result, exception, context);
    }
    
    Task HandleAsync(TMessage message, TResult? result, Exception? exception, IExecutionContext context);
}