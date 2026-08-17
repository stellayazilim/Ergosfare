
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Handles messages of type <typeparamref name="TMessage"/> asynchronously without
/// producing a result.
/// </summary>
/// <typeparam name="TMessage">The message type this handler accepts.</typeparam>
/// <remarks>
/// This is a contract in its own right, not a specialization of
/// <see cref="IHandler{TMessage, TResult}"/>. A handler that already holds a
/// <see cref="Task"/> can wrap it with <c>new ValueTask(task)</c>; an <c>async</c> method
/// body needs nothing special.
/// </remarks>
public interface IAsyncHandler<in TMessage>: IHandler
    where TMessage : notnull
{
    /// <summary>
    /// Handles <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>A task that completes when handling is done.</returns>
    ValueTask HandleAsync(TMessage message, ErgosfareContext context);
}
