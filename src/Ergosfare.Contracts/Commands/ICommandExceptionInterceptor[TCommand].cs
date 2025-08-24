using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandExceptionInterceptor<in TCommand>: ICommand,  IAsyncExceptionInterceptor<TCommand, object> where TCommand : ICommand;