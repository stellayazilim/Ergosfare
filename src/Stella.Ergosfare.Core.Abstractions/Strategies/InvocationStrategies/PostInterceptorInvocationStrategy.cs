using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes the pre-merged post-interceptor list (direct first, then indirect) for a message,
/// dispatching every interceptor through its typed contract — result-typed asynchronous
/// interceptors via <see cref="IAsyncPostInterceptor{TMessage, TResult}"/>, result-agnostic
/// ones via <see cref="IAsyncPostInterceptor{TMessage}"/>, synchronous ones via
/// <see cref="IPostInterceptor{TMessage, TResult}"/>. There is no object-typed bridge and no
/// boxed awaitable; `in` variance on both type parameters admits interceptors registered for
/// base message or result types.
/// </summary>
/// <typeparam name="TMessage">The dispatch message type (the runtime type on executor paths).</typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type — <see cref="ValueTask"/> for void pipelines, where the
/// completed-task box stands in as the (meaningless) result object.
/// </typeparam>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; interceptor contracts declare notnull results
internal sealed class PostInterceptorInvocationStrategy<TMessage, TResult>(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService,
    IServiceProvider serviceProvider)
    where TMessage : notnull
{
    /// <summary>
    /// Executes all post-interceptors for the specified message and result.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result produced by the pipeline so far.</param>
    /// <param name="context">The execution context for the current pipeline invocation.</param>
    /// <returns>The (possibly replaced) result after all post-interceptors have executed.</returns>
    public async ValueTask<object?> Invoke(TMessage message, object? result, IExecutionContext context)
    {
        var interceptors = messageDependencies.PostInterceptors;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

            result = interceptor switch
            {
                IAsyncPostInterceptor<TMessage, TResult> typedAsyncInterceptor =>
                    await typedAsyncInterceptor.HandleAsync(message, (TResult)result!, context),
                IAsyncPostInterceptor<TMessage> asyncInterceptor =>
                    await asyncInterceptor.HandleAsync(message, result!, context),
                IPostInterceptor<TMessage, TResult> syncInterceptor =>
                    syncInterceptor.Handle(message, (TResult)result!, context),
                _ => throw new NotSupportedException(
                    $"'{interceptor.GetType()}' does not implement a supported post-interceptor contract for message '{typeof(TMessage)}' and result '{typeof(TResult)}'. " +
                    "Interface-erased dispatch is not supported; dispatch with the concrete message type."),
            };

            var ex = resultAdapterService?.LookupException(result);

            if (ex != null)
            {
                throw ex;
            }
        }

        return result;
    }
}
#pragma warning restore CS8714
