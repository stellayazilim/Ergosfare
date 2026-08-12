using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes the pre-merged exception-interceptor list (direct first, then indirect) for a
/// message, dispatching every interceptor through its typed contract — result-typed
/// asynchronous interceptors via <see cref="IAsyncExceptionInterceptor{TMessage, TResult}"/>,
/// result-agnostic ones via <see cref="IAsyncExceptionInterceptor{TMessage}"/>, synchronous
/// ones via <see cref="IExceptionInterceptor{TMessage, TResult}"/>. There is no object-typed
/// bridge and no boxed awaitable; `in` variance admits interceptors registered for base
/// message or result types.
/// <para>
/// An interceptor carrying an <see cref="IExceptionInterceptorFilter"/> runs only for the
/// exceptions it accepts. The stage itself never rethrows: it reports whether any
/// interceptor <em>matched</em> — the caller owns the unhandled-failure outcome, which
/// differs by channel (rethrow for classic pipelines, a failed carrier for materializable
/// result types, nothing for a value-carried failure that already lives in the result).
/// </para>
/// </summary>
/// <typeparam name="TMessage">The dispatch message type (the runtime type on executor paths).</typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type — <see cref="Unit"/> for pipelines that produce no result.
/// </typeparam>
/// <remarks>
/// Static: the pipeline state travels as arguments, so a dispatch allocates no invoker object.
/// </remarks>
internal static class ExceptionInterceptorInvocationStrategy<TMessage, TResult>
    where TMessage : notnull
{
    /// <summary>
    /// Executes all exception interceptors for the specified message, result, and exception.
    /// </summary>
    /// <param name="messageDependencies">The message's pipeline composition, supplying the exception-interceptor list.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; interceptors resolve from it.</param>
    /// <param name="message">The message whose processing failed.</param>
    /// <param name="result">The result produced by the pipeline so far, if any.</param>
    /// <param name="exception">The failure — thrown by the pipeline or carried inside its result.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// Whether any interceptor accepted the exception, and the (possibly replaced) result
    /// after all matching exception interceptors have executed. The result is meaningful
    /// only when <c>Matched</c> is <c>true</c> — an unmatched stage ran nobody and handled
    /// nothing.
    /// </returns>
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

            // A filtered interceptor that rejects this exception is not a participant at
            // all: it neither runs nor counts towards "something handled it". Resolution
            // still happens first — the filter lives on the instance, and resolving every
            // registered interceptor is the behavior the unfiltered stage already had.
            if (interceptor is IExceptionInterceptorFilter filter && !filter.Matches(exception))
            {
                continue;
            }

            matched = true;

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
