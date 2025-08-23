using System;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IExceptionInterceptor<in TMessage, in TResult> : IExceptionInterceptor
    where TMessage : notnull
{
    
    object IExceptionInterceptor.Handle(object message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult?) messageResult, exception,  context);
    }
    
    object Handle(TMessage message,  TResult? messageResult, Exception exception,  IExecutionContext context);

}