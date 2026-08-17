
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Runs before the main handler of a <typeparamref name="TMessage"/> asynchronously, and
/// decides what the rest of the pipeline sees.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <remarks>
/// This is a contract in its own right; it does not extend the synchronous
/// <see cref="IPreInterceptor{TMessage}"/>, and an interceptor implements one of the two.
/// </remarks>
public interface IAsyncPreInterceptor<in TMessage> : IPreInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Processes <paramref name="message"/> before it reaches the main handler.
    /// </summary>
    /// <param name="message">The message as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The message the rest of the pipeline receives — either <paramref name="message"/>
    /// or a replacement. The produced value must be a <typeparamref name="TMessage"/>; the
    /// pipeline casts it before passing it on.
    /// </returns>
    ValueTask<object> HandleAsync(TMessage message, ErgosfareContext context);
}
