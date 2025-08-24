using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandPostInterceptor<in TCommand, in TResult>: ICommand, IAsyncPostInterceptor<TCommand, TResult> where TCommand : ICommand<TResult> where TResult : notnull;