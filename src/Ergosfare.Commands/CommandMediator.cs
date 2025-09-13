
using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Strategies;

namespace Ergosfare.Commands;

public class CommandMediator(
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IMessageMediator messageMediator) : ICommandMediator
{
    public Task SendAsync(ICommand commandConstruct, CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        commandMediationSettings ??= new CommandMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<ICommand>();

        var options = new MediateOptions<ICommand, Task>
        {
            MessageMediationStrategy = mediationStrategy,
            MessageResolveStrategy = messageResolveStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings.Items,
            Groups = commandMediationSettings.Filters.Groups
        };

        return messageMediator.Mediate(commandConstruct, options);
    }



    public Task<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        commandMediationSettings ??= new CommandMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<ICommand<TResult>, TResult>();

        var options = new MediateOptions<ICommand<TResult>, Task<TResult>>
        {
            MessageResolveStrategy = messageResolveStrategy,
            MessageMediationStrategy = mediationStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings.Items,
            Groups = commandMediationSettings.Filters.Groups
        };

        return messageMediator.Mediate(commandConstruct, options);
    }
}