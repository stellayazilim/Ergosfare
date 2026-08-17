using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Runs a message's final-interceptor stage, letting each interceptor observe how the
/// pipeline settled.
/// </summary>
/// <typeparam name="TMessage">
/// The message type the stage dispatches as; on executor paths this is the message's
/// runtime type.
/// </typeparam>
/// <typeparam name="TResult">
/// The pipeline's result type. Void pipelines pass <see cref="ValueTask"/>, whose value
/// stands in for a result that carries no meaning.
/// </typeparam>
/// <remarks>
/// Final interceptors return nothing, so the stage cannot change the outcome it reports.
/// </remarks>
internal static class FinalInterceptorInvocationStrategy<TMessage, TResult>
    where TMessage : notnull
{
    /// <summary>
    /// Runs every final interceptor of the message with the settled outcome.
    /// </summary>
    /// <param name="messageDependencies">The message's participants; supplies the stage list.</param>
    /// <param name="serviceProvider">The provider interceptors are resolved from.</param>
    /// <param name="message">The message that was dispatched.</param>
    /// <param name="result">The result, if the pipeline produced one.</param>
    /// <param name="exception">The failure that ended the pipeline, if it failed.</param>
    /// <param name="executionContext">The execution context of this dispatch.</param>
    /// <exception cref="NotSupportedException">
    /// An interceptor in the list implements no final-interceptor contract for
    /// <typeparamref name="TMessage"/> and <typeparamref name="TResult"/>.
    /// </exception>
    public static async ValueTask Invoke(
        IMessageDependencies messageDependencies,
        IServiceProvider serviceProvider,
        TMessage message,
        object? result,
        Exception? exception,
        ErgosfareContext executionContext)
    {
        var interceptors = messageDependencies.FinalInterceptors;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

            // Most specific contract first: an interceptor implementing several is
            // dispatched through the result-typed asynchronous one.
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
