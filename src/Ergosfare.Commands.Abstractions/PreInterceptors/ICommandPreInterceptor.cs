using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPreInterceptor: ICommand, IAsyncPreInterceptor<ICommand>;