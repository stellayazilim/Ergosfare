using System.Runtime.CompilerServices;
using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;



public interface IAsyncHandler<in TMessage>: IHandler<TMessage, Task>
    where TMessage : notnull
{
    Task IHandler<TMessage, Task>.Handle(TMessage message, IExecutionContext context)
    {
        return HandleAsync(message, context,  AmbientExecutionContext.Current.CancellationToken);
    }
        
        
    Task HandleAsync(TMessage message, IExecutionContext context, CancellationToken cancellationToken = default);
}