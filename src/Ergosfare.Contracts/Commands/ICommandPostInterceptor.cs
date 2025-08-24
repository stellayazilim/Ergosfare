using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandPostInterceptor: ICommand, IAsyncPostInterceptor<ICommand>;