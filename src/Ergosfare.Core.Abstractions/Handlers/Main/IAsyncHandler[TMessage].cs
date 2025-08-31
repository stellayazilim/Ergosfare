using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;


public interface IAsyncHandler<in TMessage>: IHandler<TMessage, Task>
    where TMessage : notnull
{
    Task IHandler<TMessage, Task>.Handle(TMessage message, IExecutionContext context)
    {
        return HandleAsync(message, context);
    }
        
        
    Task HandleAsync(TMessage message, IExecutionContext context);
}