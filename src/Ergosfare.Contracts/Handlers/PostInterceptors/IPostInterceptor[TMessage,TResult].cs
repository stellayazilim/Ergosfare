using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IPostInterceptor<in TMessage,in TResult>
    : IPostInterceptor where TMessage : notnull
{


    object IPostInterceptor.Handle(object message, object? messageResult, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult?) messageResult, AmbientExecutionContext.Current);
    }

    object Handle(TMessage message, TResult? messageResult, IExecutionContext context);

}