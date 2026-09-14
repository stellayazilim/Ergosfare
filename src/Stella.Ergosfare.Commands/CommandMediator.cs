using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands;

/// <summary>
/// The command mediator an application resolves: it holds the scope it was resolved from and
/// hands every send to the container's dispatch engine.
/// </summary>
/// <remarks>
/// The engine is shared across the process and this facade is the only object built per
/// resolution.
/// </remarks>
public class CommandMediator : ICommandMediator
{
    /// <summary>
    /// The container's dispatch engine, shared by every scope.
    /// </summary>
    private readonly MessageDispatchEngine _engine;

    /// <summary>
    /// The provider of the scope this facade was resolved from; participants resolve
    /// against it.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes the facade over a container's engine and the scope it serves.
    /// </summary>
    /// <param name="engine">The container's dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    internal CommandMediator(MessageDispatchEngine engine, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask SendAsync(ICommand commandConstruct, GroupSet groups,
        CancellationToken cancellationToken = default)
        => _engine.DispatchAsync(commandConstruct, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, GroupSet groups,
        CancellationToken cancellationToken = default)
        => _engine.DispatchAsync<TResult>(commandConstruct, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));

    /// <inheritdoc />
    public ValueTask SendAsync(ICommand commandConstruct, ErgosfareContext context, GroupSet? groups = null)
        => _engine.DispatchAsync(commandConstruct, context, _serviceProvider, groups);

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, ErgosfareContext context,
        GroupSet? groups = null)
        => _engine.DispatchAsync<TResult>(commandConstruct, context, _serviceProvider, groups);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> through its default pipeline.
    /// </summary>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <remarks>
    /// The conveniences are declared on this class as well as on the interface. A call made
    /// through the concrete type does not find a default interface method, so declaring them
    /// only on the interface would leave those calls without an overload to bind to.
    /// </remarks>
    public ValueTask SendAsync(ICommand commandConstruct, CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, GroupSet.Empty, cancellationToken);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> through its default pipeline and returns
    /// its result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, GroupSet.Empty, cancellationToken);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> naming both its own type and its result, so
    /// the pipeline is found through a static generic field rather than a lookup on the
    /// command's runtime type.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">The groups to run; an empty set runs the default group.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => _engine.DispatchAsync<TCommand, TResult>(commandConstruct, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under a caller-owned context, naming both
    /// types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; an empty set runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct, ErgosfareContext context,
        GroupSet? groups = null)
        where TCommand : ICommand<TResult>
        => _engine.DispatchAsync<TCommand, TResult>(commandConstruct, context, _serviceProvider, groups);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> through its default pipeline, naming both
    /// types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(commandConstruct, GroupSet.Empty, cancellationToken);

}
