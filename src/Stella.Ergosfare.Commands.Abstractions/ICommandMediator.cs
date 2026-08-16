using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
///     Represents the mediator interface for sending commands within the application.
/// </summary>
/// <remarks>
///     <para>
///         The command mediator is responsible for routing commands to their appropriate handlers
///         and orchestrating the command handling pipeline. It ensures that commands are processed
///         by exactly one handler and provides methods for sending commands both with and without
///         expected results.
///     </para>
///     <para>
///         Everything a dispatch can be told is a parameter. A settings object used to carry the
///         same two things, and carrying them that way meant allocating one per dispatch and
///         reading it at dispatch time — a shape nothing can be compiled from. The conveniences
///         below are default implementations over the full calls, so an implementation writes
///         four methods and inherits the rest.
///     </para>
/// </remarks>
public interface ICommandMediator
{
    /// <summary>
    ///     Sends a command that produces no result to its handler.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="groups">
    ///     The group filter, or <c>null</c> for the default pipeline. A reused
    ///     <see cref="GroupSet"/> matches the cached pipeline on a single reference check.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask SendAsync(ICommand command, IEnumerable<string>? groups, CancellationToken cancellationToken);

    /// <summary>Result-producing counterpart of the full void send.</summary>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, IEnumerable<string>? groups,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Sends under an externally owned execution context — the nested-dispatch path: a
    ///     handler opens a scope on its own context (<c>using var scope = context.CreateScope();</c>)
    ///     and passes <c>scope.Context</c> here. The caller owns the context's lifetime;
    ///     cancellation flows from the context.
    /// </summary>
    ValueTask SendAsync(ICommand command, ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>Result-producing counterpart of the context send.</summary>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, ErgosfareContext context,
        IEnumerable<string>? groups = null);

    /// <summary>Sends a command through its default pipeline.</summary>
    ValueTask SendAsync(ICommand command, CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>Result-producing counterpart of <see cref="SendAsync(ICommand, CancellationToken)"/>.</summary>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)null, cancellationToken);



    /// <summary>
    ///     Sends under a canonical group filter. Define the set once, statically, and the cached
    ///     pipeline matches it on a single reference check; <see cref="GroupSet.Empty"/>
    ///     dispatches the default pipeline.
    /// </summary>
    ValueTask SendAsync(ICommand command, GroupSet groups, CancellationToken cancellationToken = default)
        => SendAsync(command, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>Result-producing counterpart of the canonical group-filter send.</summary>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, GroupSet groups,
        CancellationToken cancellationToken = default)
        => SendAsync(command, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>Sends under a group filter given as a plain array.</summary>
    ValueTask SendAsync(ICommand command, string[] groups, CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>Result-producing counterpart of the array group-filter send.</summary>
    ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, string[] groups,
        CancellationToken cancellationToken = default)
        => SendAsync(command, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    ///     Sends a command whose own type is named alongside its result, so the dispatch
    ///     reaches its pipeline through a compile-time constant pair rather than reading the
    ///     command's type back at run time.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every other lane already takes the message as its type argument; this one could
    ///         not, because <typeparamref name="TResult"/> has to be a type parameter for the
    ///         return type and C# does not infer type arguments through constraints. Naming
    ///         both is the price, and it is why these are additions rather than replacements:
    ///         <c>SendAsync&lt;TResult&gt;(ICommand&lt;TResult&gt;)</c> stays the terse form,
    ///         and dispatching a command read off a queue is a legitimate shape whose concrete
    ///         type genuinely is a run-time fact.
    ///     </para>
    ///     <para>
    ///         Default implementations over the untyped calls, so an existing implementation
    ///         keeps compiling and simply forwards. What is gained is gained by overriding
    ///         them — <c>CommandMediator</c> does.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TCommand">The command's own type.</typeparam>
    /// <typeparam name="TResult">The result the command declares.</typeparam>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResult>
        => SendAsync<TResult>(command, groups, cancellationToken);

    /// <summary>Typed counterpart of the context send.</summary>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        where TCommand : ICommand<TResult>
        => SendAsync<TResult>(command, context, groups);

    /// <summary>Typed send through the default pipeline.</summary>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(command, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>Typed counterpart of the canonical group-filter send.</summary>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(command, groups.Count == 0 ? null : (IEnumerable<string>?)groups,
            cancellationToken);

    /// <summary>Typed counterpart of the array group-filter send.</summary>
    ValueTask<TResult> SendAsync<TCommand, TResult>(TCommand command, string[] groups,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
        => SendAsync<TCommand, TResult>(command, (IEnumerable<string>?)groups, cancellationToken);
}
