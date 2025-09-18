using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Core.Abstractions.Strategies;

namespace Ergosfare.Commands;

public class CommandMediator(
    ISignalHub signalHub,
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService,
    IMessageMediator messageMediator) : ICommandMediator
{
    
    public Task SendAsync(ICommand commandConstruct, CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        BeginPipelineSignal.Invoke(commandConstruct, null);
        
        commandMediationSettings ??= new CommandMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<ICommand>(resultAdapterService);

        var options = new MediateOptions<ICommand, Task>
        {
            MessageMediationStrategy = mediationStrategy,
            MessageResolveStrategy = messageResolveStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings.Items,
            Groups = commandMediationSettings.Filters.Groups
        };
        var result =  messageMediator.Mediate(commandConstruct, options);
        FinishPipelineSignal.Invoke(commandConstruct, result);
        return result;
    }



    public Task<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        commandMediationSettings ??= new CommandMediationSettings();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<ICommand<TResult>, TResult>(resultAdapterService);

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