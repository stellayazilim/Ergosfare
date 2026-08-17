
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Runs once a <typeparamref name="TMessage"/> pipeline has settled, whether it produced a
/// result or failed — for cleanup, auditing or logging.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type this interceptor accepts.</typeparam>
/// <remarks>
/// A final interceptor observes the outcome and cannot change it, which is why
/// <see cref="Handle"/> returns nothing. A pipeline stopped by
/// <see cref="ErgosfareContext.Abort()"/> runs no final interceptors. Implement
/// <see cref="IAsyncFinalInterceptor{TMessage}"/> or
/// <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/> instead when the work involves
/// awaiting.
/// </remarks>
public interface IFinalInterceptor<in TMessage, in TResult> : IFinalInterceptor
{
    /// <summary>
    /// Observes how the pipeline for <paramref name="message"/> settled.
    /// </summary>
    /// <param name="message">The message that was dispatched.</param>
    /// <param name="result">The result, or <c>null</c> when the pipeline failed.</param>
    /// <param name="exception">
    /// The failure that ended the pipeline, or <c>null</c> when it succeeded.
    /// </param>
    /// <param name="executionContext">The execution context of this dispatch.</param>
    void Handle(TMessage message, TResult? result, Exception? exception, ErgosfareContext executionContext);
}
