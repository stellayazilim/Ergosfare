
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Runs before the main handler of a <typeparamref name="TMessage"/> and decides what the
/// rest of the pipeline sees.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <remarks>
/// Use this to inspect, validate or replace a message. Implement
/// <see cref="IAsyncPreInterceptor{TMessage}"/> instead when the work involves awaiting;
/// the two are separate contracts and an interceptor implements one of them.
/// </remarks>
public interface IPreInterceptor<in TMessage> : IPreInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Processes <paramref name="message"/> before it reaches the main handler.
    /// </summary>
    /// <param name="message">The message as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The message the rest of the pipeline receives — either <paramref name="message"/>
    /// or a replacement. The returned value must be a <typeparamref name="TMessage"/>; the
    /// pipeline casts it before passing it on.
    /// </returns>
    object Handle(TMessage message, ErgosfareContext context);
}
