using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandPostInterceptor<in TCommand>: ICommand, IAsyncPostInterceptor<TCommand>  where TCommand : ICommand;