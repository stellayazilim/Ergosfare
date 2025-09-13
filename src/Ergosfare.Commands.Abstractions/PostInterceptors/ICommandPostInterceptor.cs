using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPostInterceptor: ICommand, IAsyncPostInterceptor<ICommand>;