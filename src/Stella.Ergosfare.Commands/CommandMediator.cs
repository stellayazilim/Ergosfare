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
    /// Mediation strategies for typed commands, one per result type; stateless, so shared
    /// across calls. Created lazily so mediators in short-lived scopes that never send
    /// typed commands pay nothing.
    /// </summary>
    private ConcurrentDictionary<Type, object>? _typedMediationStrategies;

    /// <summary>
    /// Mediates command messages through the configured pipeline, including signal dispatching,
    /// mediation strategies, and optional result adaptation.
    /// </summary>
    public ValueTask SendAsync(ICommand commandConstruct, CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {

        var options = new MediateOptions<ICommand, ValueTask>
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
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation and containing the command result.</returns>

    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        var typedMediationStrategies = LazyInitializer.EnsureInitialized(ref _typedMediationStrategies);

        if (!typedMediationStrategies.TryGetValue(typeof(TResult), out var mediationStrategy))
        {
            mediationStrategy = typedMediationStrategies.GetOrAdd(typeof(TResult),
                new SingleAsyncHandlerMediationStrategy<ICommand<TResult>, TResult>(resultAdapterService));
        }

        var options = new MediateOptions<ICommand<TResult>, ValueTask<TResult>>
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
