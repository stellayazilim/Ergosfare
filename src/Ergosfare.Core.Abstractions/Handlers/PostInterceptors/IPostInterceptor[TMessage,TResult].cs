using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IPostInterceptor<in TMessage,in TResult>
    : IPostInterceptor where TMessage : notnull
{


    object IPostInterceptor.Handle(object message, object? messageResult, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult?) messageResult, AmbientExecutionContext.Current);
    }

    object Handle(TMessage message, TResult? messageResult, IExecutionContext context);

}