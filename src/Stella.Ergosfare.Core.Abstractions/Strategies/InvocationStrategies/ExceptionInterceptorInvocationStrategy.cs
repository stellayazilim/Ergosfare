using System.Runtime.ExceptionServices;
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
/// exceptions it accepts. When no interceptor <em>matches</em> — none registered, or every
/// registered one filtered the exception out — the captured exception is rethrown with its
/// original stack.
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
    /// <param name="message">The message whose processing threw.</param>
    /// <param name="result">The result produced by the pipeline so far, if any.</param>
    /// <param name="exceptionDispatchInfo">The captured exception; rethrown when no interceptor matches it.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>The (possibly replaced) result after all matching exception interceptors have executed.</returns>
    public static async ValueTask<object?> Invoke(
        IMessageDependencies messageDependencies,
        IServiceProvider serviceProvider,
        TMessage message,
        object? result,
        ExceptionDispatchInfo exceptionDispatchInfo,
        IExecutionContext executionContext)
    {
        var interceptors = messageDependencies.ExceptionInterceptors;
        var exception = exceptionDispatchInfo.SourceException;
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

        // Nobody accepted the exception, so nobody handled it. Rethrowing the captured
        // exception hands the caller the original stack — the same outcome an empty stage
        // produces, and the reason a filtered interceptor may never be counted by presence
        // alone: that would swallow every exception it declined.
        if (!matched)
        {
            exceptionDispatchInfo.Throw();
        }

        return result;
    }
}
