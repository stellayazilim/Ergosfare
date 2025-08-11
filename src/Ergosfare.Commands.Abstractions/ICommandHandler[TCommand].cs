using Ergosfare.Messaging.Abstractions;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandHandler<in TCommand> : ICommandConstruct, IAsyncHandler<TCommand>
    where TCommand : ICommand
{
    
}