using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// A result-agnostic exception interceptor for a specific command type that runs only for
/// exceptions of type <typeparamref name="TException"/>. The exception arrives already
/// typed — no <c>is</c> check in the interceptor body.
/// </summary>
/// <typeparam name="TCommand">The type of command being intercepted. Must implement <see cref="ICommand"/>.</typeparam>
/// <typeparam name="TException">
/// The exception type this interceptor accepts, matched with <c>catch</c> semantics:
/// derived exception types match too.
/// </typeparam>
/// <remarks>
/// The result-agnostic base is deliberate, for the same reason it is on
/// <see cref="ICommandExceptionInterceptor{TCommand}"/>: a result-typed base is invisible to
/// the pipeline's pattern match whenever the pipeline result is a value type. For a
/// strongly-typed result use
/// <see cref="ICommandExceptionInterceptorFor{TCommand, TResult, TException}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptorFor<in TCommand, TException> :
    ICommand, IAsyncExceptionInterceptor<TCommand>, IExceptionInterceptorFilter<TException>
    where TCommand : ICommand
    where TException : Exception
{
    /// <inheritdoc />
    async ValueTask<object> IAsyncExceptionInterceptor<TCommand>.HandleAsync(
        TCommand command, object? messageResult, Exception exception, IExecutionContext context)
        // The cast cannot fail: the exception stage runs this interceptor only after its
        // filter accepted the exception.
        => await HandleAsync(command, messageResult, (TException)exception, context);

    /// <summary>
    /// Handles the exception asynchronously, potentially replacing the pipeline result.
    /// </summary>
    /// <param name="command">The command being processed when the exception occurred.</param>
    /// <param name="messageResult">The result produced before the exception occurred, if any.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="ValueTask{Object}"/> producing the result that continues through the pipeline.
    /// </returns>
    ValueTask<object> HandleAsync(TCommand command, object? messageResult, TException exception, IExecutionContext context);
}
