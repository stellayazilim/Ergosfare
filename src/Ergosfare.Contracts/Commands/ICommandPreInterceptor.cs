using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandPreInterceptor: ICommand, IAsyncPreInterceptor<ICommand>;