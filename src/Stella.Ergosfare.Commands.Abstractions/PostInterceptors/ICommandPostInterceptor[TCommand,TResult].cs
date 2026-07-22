using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a type-safe post-interceptor for commands with a strongly-typed result.
/// Executes after the command handler has completed and can modify the result before it
/// propagates further through the pipeline.
/// </summary>
/// <typeparam name="TCommand">The command type being intercepted. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">
/// The result type of the command. Also the type returned by the interceptor — for a
/// narrower return type there is no third parameter anymore; return the base result type.
/// </typeparam>
/// <remarks>
/// The type parameters are deliberately invariant: the pipeline invokes interceptors
/// through the non-generic root interfaces, so interface variance bought nothing while
/// forcing the result-returning member onto a separate three-parameter interface.
/// </remarks>
public interface ICommandPostInterceptor<TCommand, TResult> :
    ICommand,
    IAsyncPostInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    /// <inheritdoc />
    async ValueTask<object> IAsyncPostInterceptor<TCommand, TResult>.HandleAsync(
        TCommand command, TResult messageResult, IExecutionContext context)
        => (await HandleAsync(command, messageResult, context))!;

    /// <summary>
    /// Handles the post-processing of a command asynchronously.
    /// </summary>
    /// <param name="command">The command that was executed.</param>
    /// <param name="commandResult">The result produced by the command handler.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> producing the (possibly modified) result that
    /// continues through the pipeline.
    /// </returns>
    new ValueTask<TResult> HandleAsync(TCommand command, TResult commandResult, IExecutionContext context);
}
