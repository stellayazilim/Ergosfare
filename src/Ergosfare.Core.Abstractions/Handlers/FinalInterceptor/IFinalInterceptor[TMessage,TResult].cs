using System;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;

public interface IFinalInterceptor<in TMessage,in TResult> : IFinalInterceptor
{
    
    object IFinalInterceptor.Handle(object message, object? messageResult, Exception? exception, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult?) messageResult, exception,  context);
    }

    
    object Handle(TMessage message, TResult? result, Exception? exception, IExecutionContext executionContext);
}