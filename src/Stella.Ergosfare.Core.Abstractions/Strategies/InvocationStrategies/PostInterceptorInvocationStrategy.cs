using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Runs a message's post-interceptor stage, threading the result through each interceptor
/// in turn and stopping early on a failure carried inside one of their results.
/// </summary>
/// <typeparam name="TMessage">
/// The message type the stage dispatches as; on executor paths this is the message's
/// runtime type.
/// </typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type. Void pipelines pass <see cref="ValueTask"/>, whose value
/// stands in for a result that carries no meaning.
/// </typeparam>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; interceptor contracts declare notnull results
internal static class PostInterceptorInvocationStrategy<TMessage, TResult>
    where TMessage : notnull
{
    /// <summary>
    /// Runs the post-interceptors of the message, passing each the result the previous one
    /// returned.
    /// </summary>
    /// <param name="messageDependencies">The message's participants; supplies the stage list.</param>
    /// <param name="resultAdapter">
    /// The result type's adapter, used to spot a failure carried inside an interceptor's
    /// result; <c>null</c> when the result type has no adapter, in which case results are
    /// never inspected.
    /// </param>
    /// <param name="serviceProvider">The provider interceptors are resolved from.</param>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result as it enters the stage.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result the stage produced, and the failure carried inside it, if any. A carried
    /// failure ends the stage at the interceptor that produced it, leaving the remaining
    /// interceptors unrun, exactly as a thrown failure would. Nothing is thrown here; the
    /// caller decides how to enter the exception stage.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// An interceptor in the list implements no post-interceptor contract for
    /// <typeparamref name="TMessage"/> and <typeparamref name="TResult"/>.
    /// </exception>
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

            // Most specific contract first: an interceptor implementing several is
            // dispatched through the result-typed asynchronous one.
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
