using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IPreInterceptor<in TMessage> : IPreInterceptor 
    where TMessage : notnull
{

    object IPreInterceptor.Handle(object message,  IExecutionContext context)
    {
        return Handle((TMessage)message, AmbientExecutionContext.Current);
    }
    object Handle(TMessage message, IExecutionContext context);
}


