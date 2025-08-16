namespace Ergosfare.Contracts;

public interface ICommandHandler<in TCommand, TResult>: ICommand, IAsyncHandler<TCommand, TResult> 
    where TCommand : ICommand<TResult>;