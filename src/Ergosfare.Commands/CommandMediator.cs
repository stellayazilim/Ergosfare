using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Core.Abstractions.Strategies;

namespace Ergosfare.Commands;

/// <summary>
/// Mediates command messages through the configured pipeline, including signal dispatching,
/// mediation strategies, and optional result adaptation.
/// </summary>
public class CommandMediator(
    ISignalHub signalHub,
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService,
    IMessageMediator messageMediator) : ICommandMediator
{
    /// <summary>
    /// Mediates command messages through the configured pipeline, including signal dispatching,
    /// mediation strategies, and optional result adaptation.
    /// </summary>
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

    /// <summary>
    /// Sends a typed command asynchronously and returns a strongly typed result.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the command.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="commandMediationSettings">Optional settings for command mediation, such as filtering or additional items.</param>
    /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation and containing the command result.</returns>

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