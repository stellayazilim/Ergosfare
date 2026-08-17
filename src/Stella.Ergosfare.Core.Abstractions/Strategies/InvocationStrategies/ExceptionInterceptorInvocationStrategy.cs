using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Runs a message's exception-interceptor stage for a failure, skipping the interceptors
/// that filter it out, and reports whether any of them accepted it.
/// </summary>
/// <typeparam name="TMessage">
/// The message type the stage dispatches as; on executor paths this is the message's
/// runtime type.
/// </typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type. Pipelines that produce no result pass <see cref="Unit"/>.
/// </typeparam>
/// <remarks>
/// The stage never rethrows. What happens to an unaccepted failure differs by result type
/// — rethrown for an ordinary pipeline, turned into a failed carrier for a materializable
/// result type, left in the result when the failure was carried there to begin with — so
/// that decision belongs to the caller.
/// </remarks>
internal static class ExceptionInterceptorInvocationStrategy<TMessage, TResult>
    where TMessage : notnull
{
    /// <summary>
    /// Runs the exception interceptors that accept <paramref name="exception"/>, passing
    /// each the result the previous one returned.
    /// </summary>
    /// <param name="messageDependencies">The message's participants; supplies the stage list.</param>
    /// <param name="serviceProvider">The provider interceptors are resolved from.</param>
    /// <param name="message">The message whose dispatch failed.</param>
    /// <param name="result">The result produced so far, if any.</param>
    /// <param name="exception">
    /// The failure to handle, whether it was thrown or carried inside a result.
    /// </param>
    /// <param name="executionContext">The execution context of this dispatch.</param>
    /// <returns>
    /// Whether any interceptor accepted the failure, and the result the stage produced. The
    /// result means nothing when nothing matched — that stage ran no interceptor and
    /// handled nothing.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// A matching interceptor implements no exception-interceptor contract for
    /// <typeparamref name="TMessage"/> and <typeparamref name="TResult"/>.
    /// </exception>
    public static async ValueTask<(bool Matched, object? Result)> Invoke(
        IMessageDependencies messageDependencies,
        IServiceProvider serviceProvider,
        TMessage message,
        object? result,
        Exception exception,
        ErgosfareContext executionContext)
    {
        var interceptors = messageDependencies.ExceptionInterceptors;
        var matched = false;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

            // A filtered interceptor that rejects this failure takes no part in the stage:
            // it neither runs nor makes the failure handled. It is still resolved first,
            // because the filter is a member of the instance.
            if (interceptor is IExceptionInterceptorFilter filter && !filter.Matches(exception))
            {
                continue;
            }

            matched = true;

            // Most specific contract first: an interceptor implementing several is
            // dispatched through the result-typed asynchronous one.
            result = interceptor switch
            {
                IAsyncExceptionInterceptor<TMessage, TResult> typedAsyncInterceptor =>
                    await typedAsyncInterceptor.HandleAsync(message, (TResult?)result, exception, executionContext),
                IAsyncExceptionInterceptor<TMessage> asyncInterceptor =>
                    await asyncInterceptor.HandleAsync(message, result, exception, executionContext),
                IExceptionInterceptor<TMessage, TResult> syncInterceptor =>
                    syncInterceptor.Handle(message, (TResult?)result, exception, executionContext),
                _ => throw new NotSupportedException(
                    $"'{interceptor.GetType()}' does not implement a supported exception-interceptor contract for message '{typeof(TMessage)}' and result '{typeof(TResult)}'. " +
                    "Interface-erased dispatch is not supported; dispatch with the concrete message type."),
            };
        }

        return (matched, result);
    }
}
