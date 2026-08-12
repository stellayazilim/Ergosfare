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
/// <remarks>
/// Static: the pipeline state travels as arguments, so a dispatch allocates no invoker object.
/// </remarks>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; interceptor contracts declare notnull results
internal static class PostInterceptorInvocationStrategy<TMessage, TResult>
    where TMessage : notnull
{
    /// <summary>
    /// Executes all post-interceptors for the specified message and result.
    /// </summary>
    /// <param name="messageDependencies">The message's pipeline composition, supplying the post-interceptor list.</param>
    /// <param name="resultAdapter">The result type's bound adapter, surfacing a failure carried inside an interceptor's result; null when the type has none.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; interceptors resolve from it.</param>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result produced by the pipeline so far.</param>
    /// <param name="context">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// The (possibly replaced) result after the executed post-interceptors, and the failure
    /// carried inside one of their results, if any — a carried failure stops the stage at
    /// the interceptor that produced it (the remaining post-interceptors never run, exactly
    /// as a thrown failure would skip them) and the returned result is that failed carrier.
    /// The caller owns the transition into the exception stage; nothing is thrown here.
    /// </returns>
    public static async ValueTask<(object? Result, Exception? CarriedException)> Invoke(
        IMessageDependencies messageDependencies,
        IResultAdapter<TResult>? resultAdapter,
        IServiceProvider serviceProvider,
        TMessage message,
        object? result,
        ErgosfareContext context)
    {
        var interceptors = messageDependencies.PostInterceptors;

        // ReSharper disable once ForCanBeConvertedToForeach
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

            if (resultAdapter is not null && result is TResult typedResult
                && resultAdapter.TryGetException(in typedResult, out var carried) && carried is not null)
            {
                return (result, carried);
            }
        }

        return (result, null);
    }
}
#pragma warning restore CS8714
