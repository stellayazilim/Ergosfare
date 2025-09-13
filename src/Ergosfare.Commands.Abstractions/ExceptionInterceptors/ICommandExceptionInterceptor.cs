using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandExceptionInterceptor: ICommand, IAsyncExceptionInterceptor<ICommand>;