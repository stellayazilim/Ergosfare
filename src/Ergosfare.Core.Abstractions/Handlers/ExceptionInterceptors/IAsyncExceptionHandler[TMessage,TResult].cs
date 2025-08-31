using System;
using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;

public interface IAsyncExceptionInterceptor<in TMessage, in TResult>: 
    IExceptionInterceptor<TMessage, TResult> where TMessage : notnull
{

    object IExceptionInterceptor<TMessage, TResult>.Handle(
        TMessage message,
        TResult? result,
        Exception exception,
        IExecutionContext context)
    {
        return   HandleAsync(message, result, exception, context);
    }
       

    Task HandleAsync(TMessage message, TResult? result, Exception exception, IExecutionContext context);
    
}