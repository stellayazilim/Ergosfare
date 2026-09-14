
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Runs asynchronously once a <typeparamref name="TMessage"/> pipeline has settled,
/// reading the result as a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type this interceptor accepts.</typeparam>
/// <remarks>
/// A final interceptor observes the outcome and cannot change it, and a pipeline stopped
/// by <see cref="ErgosfareContext.Abort()"/> runs none. This is a contract in its own
/// right; it does not extend the synchronous
/// <see cref="IFinalInterceptor{TMessage, TResult}"/>.
/// </remarks>
public interface IAsyncFinalInterceptor<in TMessage, in TResult> : IFinalInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Observes how the pipeline for <paramref name="message"/> settled.
    /// </summary>
    /// <param name="message">The message that was dispatched.</param>
    /// <param name="result">The result, or <c>null</c> when the pipeline failed.</param>
    /// <param name="exception">
    /// The failure that ended the pipeline, or <c>null</c> when it succeeded.
    /// </param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask HandleAsync(TMessage message, TResult? result, Exception? exception, ErgosfareContext context);
}
