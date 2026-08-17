
namespace Stella.Ergosfare.Core.Abstractions;


/// <summary>
/// Dispatches a message through the pipeline registered for its runtime type.
/// </summary>
/// <remarks>
/// Each overload comes in two forms: one that creates the execution context for the
/// dispatch, and one that runs under a context the caller already owns. Use the latter for
/// a nested dispatch, so the inner pipeline observes what the outer one recorded.
/// </remarks>
public interface IMessageMediator
{
    /// <summary>
    /// Dispatches <paramref name="message"/> through its pipeline and completes once every
    /// participant has run.
    /// </summary>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="items">Items to publish on the execution context, or <c>null</c> for none.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <param name="groups">
    /// The pipeline groups to run; <c>null</c> runs the default group.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    ValueTask DispatchAsync(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null);

    /// <summary>
    /// Dispatches <paramref name="message"/> through its pipeline and returns the result the
    /// pipeline produced.
    /// </summary>
    /// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="items">Items to publish on the execution context, or <c>null</c> for none.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <param name="groups">
    /// The pipeline groups to run; <c>null</c> runs the default group.
    /// </param>
    /// <returns>The result produced for <paramref name="message"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    ValueTask<TResult> DispatchAsync<TResult>(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null);

    /// <summary>
    /// Dispatches <paramref name="message"/> under an execution context supplied by the
    /// caller, who keeps ownership of it.
    /// </summary>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="context">The context to run under. Cannot be <c>null</c>.</param>
    /// <param name="groups">
    /// The pipeline groups to run; <c>null</c> runs the default group.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    ValueTask DispatchAsync(object message, ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>
    /// Dispatches <paramref name="message"/> under a caller-owned execution context and
    /// returns the result the pipeline produced.
    /// </summary>
    /// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="context">The context to run under. Cannot be <c>null</c>.</param>
    /// <param name="groups">
    /// The pipeline groups to run; <c>null</c> runs the default group.
    /// </param>
    /// <returns>The result produced for <paramref name="message"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    ValueTask<TResult> DispatchAsync<TResult>(object message, ErgosfareContext context, IEnumerable<string>? groups = null);
}
