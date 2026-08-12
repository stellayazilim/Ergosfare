using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// A type-safe exception interceptor for commands with a strongly-typed result that runs
/// only for exceptions of type <typeparamref name="TException"/>. The exception arrives
/// already typed — no <c>is</c> check in the interceptor body.
/// </summary>
/// <typeparam name="TCommand">
/// The command type being intercepted. Must implement <see cref="ICommand{TResult}"/>.
/// </typeparam>
/// <typeparam name="TResult">
/// The result type of the command. Also the type returned by the interceptor — an
/// interceptor that declines to replace the result returns <c>null</c>.
/// </typeparam>
/// <typeparam name="TException">
/// The exception type this interceptor accepts, matched with <c>catch</c> semantics:
/// derived exception types match too.
/// </typeparam>
/// <remarks>
/// Filtering does not reorder anything: matching interceptors run in the pipeline's
/// existing order (weight descending, then type name), and the result threads through them
/// exactly as it does through the unfiltered
/// <see cref="ICommandExceptionInterceptor{TCommand, TResult}"/>. When no interceptor
/// accepts the thrown exception, it leaves the pipeline unwrapped with its original stack.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptorFor<in TCommand, TResult, TException> :
    ICommand, IAsyncExceptionInterceptor<TCommand, TResult>, IExceptionInterceptorFilter<TException>
    where TCommand : ICommand<TResult>
    where TResult : notnull
    where TException : Exception
{
    /// <inheritdoc />
    async ValueTask<object?> IAsyncExceptionInterceptor<TCommand, TResult>.HandleAsync(
        TCommand command, TResult? result, Exception exception, ErgosfareContext context)
        // The cast cannot fail: the exception stage runs this interceptor only after its
        // filter accepted the exception.
        => await HandleAsync(command, result, (TException)exception, context);

    /// <summary>
    /// Handles the exception asynchronously, potentially modifying the command result.
    /// </summary>
    /// <param name="command">The command being processed when the exception occurred.</param>
    /// <param name="result">The result produced before the exception occurred, if any.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> producing the (possibly modified) result that
    /// continues through the pipeline.
    /// </returns>
    ValueTask<TResult?> HandleAsync(TCommand command, TResult? result, TException exception, ErgosfareContext context);
}
