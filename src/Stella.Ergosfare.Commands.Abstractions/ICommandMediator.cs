using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Sends commands to their handlers.
/// </summary>
/// <remarks>
/// Everything a dispatch needs is passed as an argument. Only the four overloads that take
/// <c>IEnumerable&lt;string&gt;</c> groups or an <see cref="ErgosfareContext"/> are abstract;
/// the rest are conveniences implemented in terms of those, so an implementation writes four
/// methods and inherits the others.
/// </remarks>
public interface ICommandMediator
{
    /// <summary>
    /// Sends <paramref name="command"/> to its handler and completes when the pipeline has
    /// run.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">
    /// The groups to run; <c>null</c> runs the default group. Reusing a
    /// <see cref="GroupSet"/> lets the cached pipeline be matched by reference.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    ValueTask SendAsync(ICommand command, IEnumerable<string>? groups, CancellationToken cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> and returns the result its handler produced.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">
    /// The groups to run; <c>null</c> runs the default group.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, IEnumerable<string>? groups,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under an execution context supplied by the caller —
    /// the shape a nested send uses.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="context">
    /// The context to run under, typically a child opened with
    /// <c>using var scope = context.CreateScope();</c> and passed as <c>scope.Context</c>.
    /// The caller owns its lifetime, and cancellation comes from it.
    /// </param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    ValueTask SendAsync(ICommand command, ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>
    /// Sends <paramref name="command"/> under a caller-owned execution context and returns
    /// its result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, ErgosfareContext context,
        IEnumerable<string>? groups = null);

    /// <summary>
    /// Sends <paramref name="command"/> through its default pipeline.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    ValueTask SendAsync(ICommand command, CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> through its default pipeline and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)null, cancellationToken);



    /// <summary>
    /// Sends <paramref name="command"/> under a canonical group set.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">
    /// The groups to run. Build the set once and reuse it, and the cached pipeline is
    /// matched by reference; <see cref="GroupSet.Empty"/> runs the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    ValueTask SendAsync(ICommand command, GroupSet groups, CancellationToken cancellationToken = default)
        => SendAsync(command, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under a canonical group set and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups to run; <see cref="GroupSet.Empty"/> runs the default pipeline.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, GroupSet groups,
        CancellationToken cancellationToken = default)
        => SendAsync(command, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under groups given as an array.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    ValueTask SendAsync(ICommand command, string[] groups, CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under groups given as an array and returns its
    /// result.
    /// </summary>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, string[] groups,
        CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> naming its own type alongside its result, so the
    /// pipeline is reached through a pair of compile-time constants instead of the command's
    /// type being read back at run time.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    /// <remarks>
    /// <para>
    /// Both type arguments have to be named: <typeparamref name="TResult"/> must be a type
    /// parameter for the return type, and C# will not infer type arguments through a
    /// constraint. That is why these overloads are additions rather than replacements —
    /// <c>SendAsync&lt;TResult&gt;(ICommand&lt;TResult&gt;)</c> stays the short form, and a
    /// command read off a queue genuinely does not know its type until run time.
    /// </para>
    /// <para>
    /// The default implementation simply forwards to the untyped call, so an existing
    /// implementation keeps working; the benefit comes from overriding it, as
    /// <c>CommandMediator</c> does.
    /// </para>
    /// </remarks>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResult>
        => SendAsync<TResult>(command, groups, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under a caller-owned context, naming both types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        where TCommand : ICommand<TResult>
        => SendAsync<TResult>(command, context, groups);

    /// <summary>
    /// Sends <paramref name="command"/> through its default pipeline, naming both types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(command, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under a canonical group set, naming both types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups to run; <see cref="GroupSet.Empty"/> runs the default pipeline.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(command, groups.Count == 0 ? null : (IEnumerable<string>?)groups,
            cancellationToken);

    /// <summary>
    /// Sends <paramref name="command"/> under groups given as an array, naming both types.
    /// </summary>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result type the command declares.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, string[] groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(command, (IEnumerable<string>?)groups, cancellationToken);
}
