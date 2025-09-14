using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


public interface ICommandFinalInterceptor<in TCommand> :ICommand, IAsyncFinalInterceptor<TCommand>
    where TCommand : ICommand;