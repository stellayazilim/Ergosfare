using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Runs after the handler of a <typeparamref name="TCommand"/> and decides what result the
/// caller receives.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the command declares.</typeparam>
/// <remarks>
/// The interceptor returns the same result type it was given; to narrow a result, return
/// the declared type carrying the narrower value. <typeparamref name="TCommand"/> is
/// contravariant, so an interceptor written against a base command type also runs for the
/// commands derived from it, while <typeparamref name="TResult"/> stays invariant because
/// it is returned.
/// </remarks>
public interface ICommandPostInterceptor<in TCommand, TResult> :
    ICommand,
    IAsyncPostInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="command">The command that was handled.</param>
    /// <param name="messageResult">The result as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the typed method returned.</returns>
    async ValueTask<object> IAsyncPostInterceptor<TCommand, TResult>.HandleAsync(
        TCommand command, TResult messageResult, ErgosfareContext context)
        => (await HandleAsync(command, messageResult, context));

    /// <summary>
    /// Processes the result of handling <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command that was handled.</param>
    /// <param name="commandResult">The result as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result the rest of the pipeline receives — either the one passed in or a
    /// replacement.
    /// </returns>
    new ValueTask<TResult> HandleAsync(TCommand command, TResult commandResult, ErgosfareContext context);
}
