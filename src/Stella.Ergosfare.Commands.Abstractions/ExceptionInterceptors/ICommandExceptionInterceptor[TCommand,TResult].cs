using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a type-safe exception interceptor for commands with a strongly-typed result.
/// The interceptor can inspect the exception and modify or replace the command result.
/// </summary>
/// <typeparam name="TCommand">
/// The command type being intercepted. Must implement <see cref="ICommand{TResult}"/>.
/// </typeparam>
/// <typeparam name="TResult">
/// The result type of the command. Also the type returned by the interceptor — for a
/// narrower return type there is no third parameter anymore; return the base result type.
/// </typeparam>
/// <remarks>
/// The type parameters are deliberately invariant: the pipeline invokes interceptors
/// through the non-generic root interfaces, so interface variance bought nothing while
/// forcing the result-returning member onto a separate three-parameter interface.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptor<TCommand, TResult> : ICommand, IAsyncExceptionInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    /// <inheritdoc />
    async Task<object?> IAsyncExceptionInterceptor<TCommand, TResult>.HandleAsync(
        TCommand command, TResult? result, Exception exception, IExecutionContext context)
        => await HandleAsync(command, result, exception, context);

    /// <summary>
    /// Handles the exception asynchronously, potentially modifying the command result.
    /// </summary>
    /// <param name="command">The command being processed when the exception occurred.</param>
    /// <param name="result">The result produced before the exception occurred, if any.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing the (possibly modified) result that
    /// continues through the pipeline.
    /// </returns>
    new Task<TResult?> HandleAsync(TCommand command, TResult? result, Exception exception, IExecutionContext context);
}
