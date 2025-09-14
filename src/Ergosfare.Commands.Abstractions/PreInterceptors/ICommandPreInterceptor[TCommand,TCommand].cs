using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPreInterceptor<in TCommand,  TModifiedCommand>: ICommand, IAsyncPreInterceptor<TCommand>
    where TCommand : ICommand
    where TModifiedCommand : TCommand
{
    async Task<object> IAsyncPreInterceptor<TCommand>.HandleAsync(TCommand command, IExecutionContext context)
    {
        return await HandleAsync(command, context);
    }
    public new Task<TModifiedCommand> HandleAsync(TCommand command, IExecutionContext context);

}