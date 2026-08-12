
namespace Stella.Ergosfare.Core.Abstractions;


/// <summary>
/// Defines a mediator responsible for dispatching messages to their corresponding handlers
/// and returning results from the mediation process.
/// </summary>
public interface IMessageMediator
{
    /// <summary>
    /// Dispatches <paramref name="message"/> through the cached pipeline executor closed over
    /// its runtime type and the requested group set. The handler is invoked through its typed
    /// member — no object-typed bridge.
    /// </summary>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="items">Optional items exposed on the execution context.</param>
    /// <param name="cancellationToken">Cancellation token exposed on the execution context.</param>
    /// <param name="groups">Pipeline groups to dispatch under; <c>null</c> selects the default group.</param>
    ValueTask DispatchAsync(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null);

    /// <summary>
    /// Dispatches <paramref name="message"/> through the cached result-producing pipeline
    /// executor closed over its runtime type and the requested group set; see
    /// <see cref="DispatchAsync(object, IDictionary{object, object}, CancellationToken, IEnumerable{string})"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="items">Optional items exposed on the execution context.</param>
    /// <param name="cancellationToken">Cancellation token exposed on the execution context.</param>
    /// <param name="groups">Pipeline groups to dispatch under; <c>null</c> selects the default group.</param>
    ValueTask<TResult> DispatchAsync<TResult>(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null);

    /// <summary>
    /// Dispatches <paramref name="message"/> under an externally owned execution context —
    /// the nested-dispatch path: a handler opens a scope on its own context and passes the
    /// child here. The caller owns the context's lifetime.
    /// </summary>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="context">The externally owned execution context to dispatch under.</param>
    /// <param name="groups">Pipeline groups to dispatch under; <c>null</c> selects the default group.</param>
    ValueTask DispatchAsync(object message, ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>
    /// Result-producing counterpart of
    /// <see cref="DispatchAsync(object, ErgosfareContext, IEnumerable{string})"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="context">The externally owned execution context to dispatch under.</param>
    /// <param name="groups">Pipeline groups to dispatch under; <c>null</c> selects the default group.</param>
    ValueTask<TResult> DispatchAsync<TResult>(object message, ErgosfareContext context, IEnumerable<string>? groups = null);
}