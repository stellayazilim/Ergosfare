using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandHandler<in TCommand> : ICommand, IAsyncHandler<TCommand>
    where TCommand : ICommand;