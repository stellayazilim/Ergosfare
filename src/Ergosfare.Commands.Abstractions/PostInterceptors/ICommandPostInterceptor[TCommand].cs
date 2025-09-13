using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPostInterceptor<in TCommand>: ICommand, IAsyncPostInterceptor<TCommand>  where TCommand : ICommand;