using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


public interface ICommandFinalInterceptor<in TCommand,in TResult> : 
    ICommand, IAsyncFinalInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>;