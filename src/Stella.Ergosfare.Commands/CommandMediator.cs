using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands;

/// <summary>
/// Mediates command messages through the pipeline executor closed over the command's
/// runtime type: handlers are always invoked through their typed members, and the dispatch
/// path carries no object-typed bridge, options object, or erased strategy.
/// </summary>
public class CommandMediator : ICommandMediator
{
    /// <summary>
    /// The mediator backing the original construction shape; null when the facade is
    /// engine-backed.
    /// </summary>
    private readonly IMessageMediator? _messageMediator;

    /// <summary>
    /// The singleton dispatch engine; null when the facade wraps an
    /// <see cref="IMessageMediator"/>.
    /// </summary>
    private readonly MessageDispatchEngine? _engine;

    /// <summary>
    /// The scope provider handlers resolve against on the engine path.
    /// </summary>
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Wraps an existing <see cref="IMessageMediator"/> — the original construction shape,
    /// kept for direct construction and foreign mediator implementations.
    /// </summary>
    public CommandMediator(IMessageMediator messageMediator)
    {
        _messageMediator = messageMediator;
    }

    /// <summary>
    /// Engine-backed construction: dispatches go straight to the process-wide engine with
    /// <paramref name="serviceProvider"/> as the handler-resolution scope, making the
    /// facade the only object built per resolution.
    /// </summary>
    /// <param name="engine">The singleton dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    public CommandMediator(MessageDispatchEngine engine, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask SendAsync(ICommand commandConstruct, IEnumerable<string>? groups,
        IDictionary<object, object?>? items, CancellationToken cancellationToken)
        => _engine is not null
            ? _engine.DispatchAsync(commandConstruct, _serviceProvider!, items, cancellationToken, groups)
            : _messageMediator!.DispatchAsync(commandConstruct, items, cancellationToken, groups);

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, IEnumerable<string>? groups,
        IDictionary<object, object?>? items, CancellationToken cancellationToken)
        => _engine is not null
            ? _engine.DispatchAsync<TResult>(commandConstruct, _serviceProvider!, items, cancellationToken, groups)
            : _messageMediator!.DispatchAsync<TResult>(commandConstruct, items, cancellationToken, groups);

    /// <inheritdoc />
    public ValueTask SendAsync(ICommand commandConstruct, ErgosfareContext context, IEnumerable<string>? groups = null)
        => _engine is not null
            ? _engine.DispatchAsync(commandConstruct, context, _serviceProvider!, groups)
            : _messageMediator!.DispatchAsync(commandConstruct, context, groups);

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        => _engine is not null
            ? _engine.DispatchAsync<TResult>(commandConstruct, context, _serviceProvider!, groups)
            : _messageMediator!.DispatchAsync<TResult>(commandConstruct, context, groups);

    /// <summary>Sends a command through its default pipeline.</summary>
    /// <remarks>
    /// The conveniences are declared here as well as on the interface. They used to be
    /// extension methods, which a concrete-typed receiver finds; a default interface method is
    /// not, so carrying them only on the interface would have broken every call made through
    /// this class.
    /// </remarks>
    public ValueTask SendAsync(ICommand commandConstruct, CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, null, null, cancellationToken);

    /// <summary>Result-producing counterpart of the default send.</summary>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, null, null, cancellationToken);

    /// <summary>Sends with contextual items the pipeline can read and write.</summary>
    public ValueTask SendAsync(ICommand commandConstruct, IDictionary<object, object?> items,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, null, items, cancellationToken);

    /// <summary>Result-producing counterpart of the contextual-items send.</summary>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        IDictionary<object, object?> items, CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, null, items, cancellationToken);

    /// <summary>Sends under a canonical group filter; an empty set routes to the group-less lane.</summary>
    public ValueTask SendAsync(ICommand commandConstruct, GroupSet groups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return SendAsync(commandConstruct, groups.Count == 0 ? null : groups, null, cancellationToken);
    }

    /// <summary>Result-producing counterpart of the canonical group-filter send.</summary>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return SendAsync(commandConstruct, groups.Count == 0 ? null : groups, null, cancellationToken);
    }

    /// <summary>Sends under a group filter given as a plain array.</summary>
    public ValueTask SendAsync(ICommand commandConstruct, string[] groups,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, groups, null, cancellationToken);

    /// <summary>Result-producing counterpart of the array group-filter send.</summary>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, string[] groups,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, groups, null, cancellationToken);
}
