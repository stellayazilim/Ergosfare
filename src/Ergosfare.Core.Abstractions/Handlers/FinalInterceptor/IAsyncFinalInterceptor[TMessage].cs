using System;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;

public interface IAsyncFinalInterceptor<in TMessage>: IFinalInterceptor<TMessage, object>
{
    object IFinalInterceptor<TMessage, object>.Handle(TMessage message, object? result, Exception? exception,
        IExecutionContext context)
    {
        return HandleAsync((TMessage) message, result, exception, context);
    }
    
    Task HandleAsync(TMessage message, object? result, Exception? exception, IExecutionContext context);
}