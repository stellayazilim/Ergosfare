using Ergosfare.Commands.Abstractions;
using Ergosfare.Messaging.Abstractions;
using Ergosfare.Messaging.Abstractions.Strategies;

namespace Ergosfare.Commands;

public class CommandMediator(IMessageMediator messageMediator) : ICommandMediator
{
    public Task SendAsync(ICommandConstruct commandConstruct, CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        commandMediationSettings ??= new CommandMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<ICommandConstruct>();
        var findStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        var options = new MediateOptions<ICommandConstruct, Task>
        {
            MessageMediationStrategy = mediationStrategy,
            MessageResolveStrategy = findStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings.Items
        };

        return messageMediator.Mediate(commandConstruct, options);
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        commandMediationSettings ??= new CommandMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<ICommand<TResult>, TResult>();
        var findStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        var options = new MediateOptions<ICommand<TResult>, Task<TResult>>
        {
            MessageResolveStrategy = findStrategy,
            MessageMediationStrategy = mediationStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings.Items,
        };

        return messageMediator.Mediate(commandConstruct, options);
    }
}