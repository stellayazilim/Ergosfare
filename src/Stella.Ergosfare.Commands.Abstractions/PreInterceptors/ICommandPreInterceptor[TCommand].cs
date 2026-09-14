using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Runs before the handler of a <typeparamref name="TCommand"/> and decides which command
/// the rest of the pipeline sees.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <remarks>
/// A pre-interceptor produces no result, so this form returns the command type itself
/// rather than <see cref="object"/> — which is why <typeparamref name="TCommand"/> is
/// invariant here. Returning a derived command is allowed and needs nothing extra: it is
/// still a <typeparamref name="TCommand"/>. Use <see cref="ICommandPreInterceptor"/> to
/// accept any command instead.
/// </remarks>
public interface ICommandPreInterceptor<TCommand> : ICommand, IAsyncPreInterceptor<TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="command">The command as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The command the typed method returned.</returns>
    async ValueTask<object> IAsyncPreInterceptor<TCommand>.HandleAsync(TCommand command, ErgosfareContext context)
        => await HandleAsync(command, context);

    /// <summary>
    /// Processes <paramref name="command"/> before its handler runs.
    /// </summary>
    /// <param name="command">The command as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The command the rest of the pipeline receives — either the one passed in or a
    /// replacement.
    /// </returns>
    new ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context);
}
