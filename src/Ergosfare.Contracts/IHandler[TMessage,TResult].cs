
namespace Ergosfare.Contracts;


public interface IHandler<in TMessage, out TResult> : IHandler
    where TMessage : notnull
    where TResult : notnull
{
    
    object IHandler.Handle(object message)
    {
        return Handle((TMessage) message);
    }
    
    TResult Handle(TMessage message);
}