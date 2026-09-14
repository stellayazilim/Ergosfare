
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Runs after the main handler of a <typeparamref name="TMessage"/> and decides what
/// result the rest of the pipeline sees.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type this interceptor accepts.</typeparam>
/// <remarks>
/// Implement <see cref="IAsyncPostInterceptor{TMessage}"/> or
/// <see cref="IAsyncPostInterceptor{TMessage, TResult}"/> instead when the work involves
/// awaiting; an interceptor implements one of these contracts.
/// </remarks>
public interface IPostInterceptor<in TMessage, in TResult>
    : IPostInterceptor
        where TMessage : notnull
        where TResult : notnull
{
    /// <summary>
    /// Processes the result of handling <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="messageResult">The result as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result the rest of the pipeline receives — either
    /// <paramref name="messageResult"/> or a replacement. The returned value must be a
    /// <typeparamref name="TResult"/>; the pipeline casts it before passing it on. If the
    /// returned result carries a failure that the result type's adapter can read, the
    /// remaining post-interceptors are skipped and the pipeline moves to its exception
    /// stage.
    /// </returns>
    object Handle(TMessage message, TResult messageResult, ErgosfareContext context);
}
