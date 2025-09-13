using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandExceptionInterceptor<in TCommand>: ICommand,  IAsyncExceptionInterceptor<TCommand, object> where TCommand : ICommand;