using System.Runtime.CompilerServices;
using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;



public interface IAsyncHandler<in TMessage>: IHandler<TMessage, Task>
    where TMessage : notnull
{
    Task IHandler<TMessage, Task>.Handle(TMessage message)
    {
        return HandleAsync(message,  AmbientExecutionContext.Current.CancellationToken);
    }
        
        
    Task HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}