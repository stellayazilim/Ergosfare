using System.Collections.Concurrent;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;

namespace Stella.Ergosfare.Commands;

/// <summary>
/// Mediates command messages through the configured pipeline, including signal dispatching,
/// mediation strategies, and optional result adaptation.
/// </summary>
public class CommandMediator(
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService,
    IMessageMediator messageMediator) : ICommandMediator
{
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// Mediation strategy for non-generic commands; stateless, so shared across calls.
    /// </summary>
    private readonly SingleAsyncHandlerMediationStrategy<ICommand> _mediationStrategy = new(resultAdapterService);

    /// <summary>
    /// Mediation strategies for typed commands, one per result type; stateless, so shared across calls.
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _typedMediationStrategies = new();

    /// <summary>
    /// Mediates command messages through the configured pipeline, including signal dispatching,
    /// mediation strategies, and optional result adaptation.
    /// </summary>
    public Task SendAsync(ICommand commandConstruct, CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {

        var options = new MediateOptions<ICommand, Task>
        {
            MessageMediationStrategy = _mediationStrategy,
            MessageResolveStrategy = messageResolveStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings?.Items,
            Groups = commandMediationSettings?.Filters.Groups ?? EmptyGroups
        };
        var result =  messageMediator.Mediate(commandConstruct, options);
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
        if (!_typedMediationStrategies.TryGetValue(typeof(TResult), out var mediationStrategy))
        {
            mediationStrategy = _typedMediationStrategies.GetOrAdd(typeof(TResult),
                new SingleAsyncHandlerMediationStrategy<ICommand<TResult>, TResult>(resultAdapterService));
        }

        var options = new MediateOptions<ICommand<TResult>, Task<TResult>>
        {
            MessageResolveStrategy = messageResolveStrategy,
            MessageMediationStrategy = (SingleAsyncHandlerMediationStrategy<ICommand<TResult>, TResult>)mediationStrategy,
            CancellationToken = cancellationToken,
            Items = commandMediationSettings?.Items,
            Groups = commandMediationSettings?.Filters.Groups ?? EmptyGroups
        };

        return messageMediator.Mediate(commandConstruct, options);
    }


}
