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
    public CommandMediator(MessageDispatchEngine engine, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask SendAsync(ICommand commandConstruct, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        => _engine.DispatchAsync(commandConstruct, _serviceProvider, cancellationToken, groups);

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        => _engine.DispatchAsync<TResult>(commandConstruct, _serviceProvider, cancellationToken, groups);

    /// <inheritdoc />
    public ValueTask SendAsync(ICommand commandConstruct, ErgosfareContext context, IEnumerable<string>? groups = null)
        => _engine.DispatchAsync(commandConstruct, context, _serviceProvider, groups);

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, ErgosfareContext context,
        IEnumerable<string>? groups = null)
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
        => SendAsync(commandConstruct, (IEnumerable<string>?)null, cancellationToken);

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
        => SendAsync(commandConstruct, (IEnumerable<string>?)null, cancellationToken);



    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under a canonical group set.
    /// </summary>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">
    /// The groups to run. An empty set sends through the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="groups"/> is <c>null</c>.</exception>
    public ValueTask SendAsync(ICommand commandConstruct, GroupSet groups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return SendAsync(commandConstruct, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under a canonical group set and returns its
    /// result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">
    /// The groups to run. An empty set sends through the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="groups"/> is <c>null</c>.</exception>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return SendAsync(commandConstruct, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under groups given as an array.
    /// </summary>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    public ValueTask SendAsync(ICommand commandConstruct, string[] groups,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under groups given as an array and returns
    /// its result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> commandConstruct, string[] groups,
        CancellationToken cancellationToken = default)
        => SendAsync(commandConstruct, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> naming both its own type and its result, so
    /// the pipeline is found through a static generic field rather than a lookup on the
    /// command's runtime type.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResult>
        => _engine.DispatchAsync<TCommand, TResult>(commandConstruct, _serviceProvider, cancellationToken, groups);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under a caller-owned context, naming both
    /// types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct, ErgosfareContext context,
        IEnumerable<string>? groups = null)
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
        => SendAsync<TCommand, TResult>(commandConstruct, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under a canonical group set, naming both
    /// types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">
    /// The groups to run. An empty set sends through the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="groups"/> is <c>null</c>.</exception>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(groups);

        return SendAsync<TCommand, TResult>(commandConstruct,
            groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="commandConstruct"/> under groups given as an array, naming both
    /// types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="commandConstruct">The command to send.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand commandConstruct, string[] groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(commandConstruct, (IEnumerable<string>?)groups, cancellationToken);
}
