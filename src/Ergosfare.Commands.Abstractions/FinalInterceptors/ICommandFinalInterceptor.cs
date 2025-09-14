using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandFinalInterceptor: ICommand, IAsyncFinalInterceptor<ICommand>;