using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Defines a type-safe pre-interceptor for a command. It runs before the handler and returns
/// the command that continues through the pipeline — the original instance, or a rewritten one.
/// </summary>
/// <typeparam name="TCommand">The type of command to intercept.</typeparam>
/// <remarks>
/// A pre-interceptor carries no result, so — unlike the post/exception interceptors, whose
/// second type parameter is the result — the single-parameter form returns the command type
/// directly rather than <see cref="object"/>. Use the non-generic
/// <see cref="ICommandPreInterceptor"/> to intercept any command (returning <see cref="object"/>),
/// or <see cref="ICommandPreInterceptor{TCommand, TModifiedCommand}"/> to return a different,
/// derived command type. <typeparamref name="TCommand"/> is invariant because it is returned.
/// </remarks>
public interface ICommandPreInterceptor<TCommand> : ICommand, IAsyncPreInterceptor<TCommand>
    where TCommand : ICommand
{
    /// <inheritdoc cref="IAsyncPreInterceptor{TMessage}.HandleAsync(TMessage,ErgosfareContext)"/>
    async ValueTask<object> IAsyncPreInterceptor<TCommand>.HandleAsync(TCommand command, ErgosfareContext context)
        => await HandleAsync(command, context);

    /// <summary>
    /// Handles the command before its handler runs and returns the command that continues
    /// through the pipeline (the original, or a rewritten instance).
    /// </summary>
    /// <param name="command">The command to intercept.</param>
    /// <param name="context">The current execution context.</param>
    new ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context);
}
