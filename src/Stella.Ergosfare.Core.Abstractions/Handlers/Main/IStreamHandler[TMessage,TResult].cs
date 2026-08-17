
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Handles messages of type <typeparamref name="TMessage"/> by streaming
/// <typeparamref name="TResult"/> items back to the caller.
/// </summary>
/// <typeparam name="TMessage">The message type this handler accepts.</typeparam>
/// <typeparam name="TResult">The type of each streamed item.</typeparam>
/// <remarks>
/// The contract is <see cref="IHandler{TMessage, TResult}"/> closed over
/// <see cref="IAsyncEnumerable{T}"/>; its <c>Handle</c> is implemented explicitly here and
/// forwards to <see cref="StreamAsync"/>, so implementations only write the streaming
/// method. Items are produced as the caller enumerates, after the dispatch call itself has
/// returned.
/// </remarks>
public interface IStreamHandler<in TMessage, out TResult>
    :IHandler<TMessage, IAsyncEnumerable<TResult>>
        where TMessage : notnull
{
    /// <summary>
    /// Forwards the main-handler contract to <see cref="StreamAsync"/>.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The stream <see cref="StreamAsync"/> produced.</returns>
    IAsyncEnumerable<TResult> IHandler<TMessage, IAsyncEnumerable<TResult>>.Handle(
        TMessage message,
        ErgosfareContext context)
    {
        return StreamAsync(message, context);
    }


    /// <summary>
    /// Streams the results of handling <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The streamed results.</returns>
    IAsyncEnumerable<TResult> StreamAsync(TMessage message,ErgosfareContext context);
}
