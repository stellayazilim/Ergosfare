using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands;

/// <summary>
/// Mediates command messages through the pipeline executor closed over the command's
/// runtime type: handlers are always invoked through their typed members, and the dispatch
/// path carries no object-typed bridge, options object, or erased strategy.
/// </summary>
public class CommandMediator(IMessageMediator messageMediator) : ICommandMediator
{
    /// <summary>
    /// Sends a void command through the executor pipeline.
    /// </summary>
    public ValueTask SendAsync(ICommand commandConstruct, CommandMediationSettings? commandMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        return messageMediator.DispatchAsync(
            commandConstruct,
            commandMediationSettings?.Items,
            cancellationToken,
            commandMediationSettings?.Filters.Groups);
    }

    /// <summary>
    /// Sends a typed command through the executor pipeline and returns its result.
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
        return messageMediator.DispatchAsync<TResult>(
            commandConstruct,
            commandMediationSettings?.Items,
            cancellationToken,
            commandMediationSettings?.Filters.Groups);
    }

    /// <summary>
    /// Sends a void command under an externally owned execution context — the
    /// nested-dispatch path: a handler opens a scope on its own context and passes the
    /// child here. The caller owns the context's lifetime; cancellation flows from the
    /// context.
    /// </summary>
    public ValueTask SendAsync(ICommand commandConstruct, IExecutionContext context,
        CommandMediationSettings? commandMediationSettings = null)
    {
        return messageMediator.DispatchAsync(
            commandConstruct,
            context,
            commandMediationSettings?.Filters.Groups);
    }

    /// <summary>
    /// Result-producing counterpart of
    /// <see cref="SendAsync(ICommand, IExecutionContext, CommandMediationSettings?)"/>.
    /// </summary>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, IExecutionContext context,
        CommandMediationSettings? commandMediationSettings = null)
    {
        return messageMediator.DispatchAsync<TResult>(
            commandConstruct,
            context,
            commandMediationSettings?.Filters.Groups);
    }
}
