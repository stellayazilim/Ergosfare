using Ergosfare.Messaging.Abstractions;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandHandler<in TCommand, TResult>: ICommandConstruct, IAsyncHandler<TCommand, TResult> 
    where TCommand : ICommand<TResult>;