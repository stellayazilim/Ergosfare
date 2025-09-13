using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPostInterceptor<in TCommand, in TResult>: 
    ICommand, 
    IAsyncPostInterceptor<TCommand, TResult> 
        where TCommand : ICommand<TResult> 
        where TResult : notnull;