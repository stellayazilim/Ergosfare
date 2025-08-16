namespace Ergosfare.Contracts;

public interface ICommandHandler<in TCommand> : ICommand, IAsyncHandler<TCommand>
    where TCommand : ICommand;