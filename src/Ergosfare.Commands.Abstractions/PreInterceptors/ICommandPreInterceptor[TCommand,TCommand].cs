using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPreInterceptor<in TCommand, out TModifiedCommand>: ICommand, IPreInterceptor<TCommand>
    where TCommand : ICommand
    where TModifiedCommand : TCommand
{
    
    object IPreInterceptor<TCommand>.Handle(TCommand command, IExecutionContext context)
    {
        return Handle(command, context);
    }
    public new TModifiedCommand Handle(TCommand command, IExecutionContext context);

}