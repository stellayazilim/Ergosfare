using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandHandler<in TCommand, TResult>: ICommand, IAsyncHandler<TCommand, TResult> 
    where TCommand : ICommand<TResult>;