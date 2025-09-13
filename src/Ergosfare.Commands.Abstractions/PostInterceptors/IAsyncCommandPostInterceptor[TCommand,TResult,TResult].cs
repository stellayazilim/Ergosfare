using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface IAsyncCommandPostInterceptor<in TCommand, in TResult,  TModifiedResult> : ICommandPostInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{

    async Task<object> IAsyncPostInterceptor<TCommand, TResult>.HandleAsync(TCommand command, TResult result,
        IExecutionContext context)
    {
        return (await HandleAsync(command, result, context))!;
    }
    
    new Task<TModifiedResult> HandleAsync(TCommand command, TResult commandResult, IExecutionContext context);
}
