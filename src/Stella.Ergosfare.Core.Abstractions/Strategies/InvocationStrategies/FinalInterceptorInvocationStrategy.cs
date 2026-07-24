using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes the pre-merged final-interceptor list (direct first, then indirect) for a message,
/// dispatching every interceptor through its typed contract — result-typed asynchronous
/// interceptors via <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/>, result-agnostic
/// ones via <see cref="IAsyncFinalInterceptor{TMessage}"/>, synchronous ones via
/// <see cref="IFinalInterceptor{TMessage, TResult}"/>. There is no object-typed bridge and no
/// boxed awaitable; `in` variance admits interceptors registered for base message or result
/// types. Final interceptors observe the outcome but cannot alter it.
/// </summary>
/// <typeparam name="TMessage">The dispatch message type (the runtime type on executor paths).</typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type — <see cref="ValueTask"/> for void pipelines, where the
/// completed-task box stands in as the (meaningless) result object.
/// </typeparam>
internal sealed class FinalInterceptorInvocationStrategy<TMessage, TResult>(
    IMessageDependencies messageDependencies,
    IServiceProvider serviceProvider)
    where TMessage : notnull
{
    /// <summary>
    /// Executes all final interceptors for the specified message, result, and exception.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result, if any.</param>
    /// <param name="exception">The exception that terminated the pipeline, if any.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    public async ValueTask Invoke(TMessage message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        var interceptors = messageDependencies.FinalInterceptors;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

            switch (interceptor)
            {
                case IAsyncFinalInterceptor<TMessage, TResult> typedAsyncInterceptor:
                    await typedAsyncInterceptor.HandleAsync(message, (TResult?)result, exception, executionContext);
                    break;
                case IAsyncFinalInterceptor<TMessage> asyncInterceptor:
                    await asyncInterceptor.HandleAsync(message, result, exception, executionContext);
                    break;
                case IFinalInterceptor<TMessage, TResult> syncInterceptor:
                    syncInterceptor.Handle(message, (TResult?)result, exception, executionContext);
                    break;
                default:
                    throw new NotSupportedException(
                        $"'{interceptor.GetType()}' does not implement a supported final-interceptor contract for message '{typeof(TMessage)}' and result '{typeof(TResult)}'. " +
                        "Interface-erased dispatch is not supported; dispatch with the concrete message type.");
            }
        }
    }
}
