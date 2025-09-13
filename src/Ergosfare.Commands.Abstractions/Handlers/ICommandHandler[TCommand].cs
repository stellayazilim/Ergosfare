using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandHandler<in TCommand> : ICommand, IAsyncHandler<TCommand>
    where TCommand : ICommand;