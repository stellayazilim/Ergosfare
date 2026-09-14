
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Runs after the main handler of a <typeparamref name="TMessage"/> asynchronously,
/// without naming the result type.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <remarks>
/// Use this when the interceptor works for any result — logging or metrics, say. To read
/// or replace a typed result, implement
/// <see cref="IAsyncPostInterceptor{TMessage, TResult}"/> instead. These are separate
/// contracts and an interceptor implements one of them.
/// </remarks>
public interface IAsyncPostInterceptor<in TMessage>
    : IPostInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Processes the result of handling <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="messageResult">
    /// The result as the previous stage left it. Void pipelines pass a completed task here,
    /// which carries no meaning.
    /// </param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result the rest of the pipeline receives — either
    /// <paramref name="messageResult"/> or a replacement, which must be of the pipeline's
    /// result type. If it carries a failure the result type's adapter can read, the
    /// remaining post-interceptors are skipped and the pipeline moves to its exception
    /// stage.
    /// </returns>
    ValueTask<object> HandleAsync(TMessage message, object messageResult, ErgosfareContext context);
}
