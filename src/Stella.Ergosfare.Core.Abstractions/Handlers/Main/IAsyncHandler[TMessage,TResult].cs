
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Handles messages of type <typeparamref name="TMessage"/> asynchronously and produces a
/// <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TMessage">The message type this handler accepts.</typeparam>
/// <typeparam name="TResult">The result type this handler produces.</typeparam>
/// <remarks>
/// A handler that completes synchronously allocates nothing by returning the value
/// directly, and one that already holds a <see cref="Task{TResult}"/> can wrap it with
/// <c>new ValueTask&lt;TResult&gt;(task)</c>.
/// </remarks>
public interface IAsyncHandler<in TMessage,  TResult>: IHandler
    where TMessage : notnull
{

    /// <summary>
    /// Handles <paramref name="message"/> and produces the result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result of handling <paramref name="message"/>.</returns>
    ValueTask<TResult> HandleAsync(TMessage message, ErgosfareContext context);

}
