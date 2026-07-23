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
/// message or result types. With no interceptors registered, the captured exception is
/// rethrown with its original stack.
/// </summary>
/// <typeparam name="TMessage">The dispatch message type (the runtime type on executor paths).</typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type — <see cref="ValueTask"/> for void pipelines, where the
/// completed-task box stands in as the (meaningless) result object.
/// </typeparam>
internal sealed class ExceptionInterceptorInvocationStrategy<TMessage, TResult>(
    IMessageDependencies messageDependencies,
    IServiceProvider serviceProvider)
    where TMessage : notnull
{
    /// <summary>
    /// Executes all exception interceptors for the specified message, result, and exception.
    /// </summary>
    /// <param name="message">The message whose processing threw.</param>
    /// <param name="result">The result produced by the pipeline so far, if any.</param>
    /// <param name="exceptionDispatchInfo">The captured exception; rethrown when no interceptor is registered.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>The (possibly replaced) result after all exception interceptors have executed.</returns>
    public async ValueTask<object?> Invoke(
        TMessage message,
        object? result,
        ExceptionDispatchInfo exceptionDispatchInfo,
        IExecutionContext executionContext)
    {
        var interceptors = messageDependencies.ExceptionInterceptors;

        if (interceptors.Count == 0)
        {
            exceptionDispatchInfo.Throw();
        }

        var exception = exceptionDispatchInfo.SourceException;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

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

        return result;
    }
}
