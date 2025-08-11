using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Messaging.Abstractions.Context;

namespace Ergosfare.Messaging.Abstractions;

public interface IAsyncHandler<in TMessage>: IHandler<TMessage, Task>
        where TMessage : notnull
{
        Task IHandler<TMessage, Task>.Handle(TMessage message)
        {
                return HandleAsync(message,  AmbientExecutionContext.Current.CancellationToken);
        }
        
        
        Task HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}