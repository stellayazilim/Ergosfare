
using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;


public interface IHandler<in TMessage, out TResult> : IHandler
    where TMessage : notnull
    where TResult : notnull
{
    
    object IHandler.Handle(object message, IExecutionContext context)
    {
        return Handle((TMessage) message, context);
    }
    
    TResult Handle(TMessage message, IExecutionContext context);
}