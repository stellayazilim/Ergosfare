namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Handles messages of type <typeparamref name="TMessage"/> synchronously and returns a
/// <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TMessage">The message type this handler accepts.</typeparam>
/// <typeparam name="TResult">The result type this handler produces.</typeparam>
/// <remarks>
/// Implement <see cref="IAsyncHandler{TMessage, TResult}"/> instead when handling involves
/// awaiting; the two are separate contracts and a handler implements one of them.
/// </remarks>
public interface IHandler<in TMessage, out TResult> : IHandler
    where TMessage : notnull
    where TResult : notnull
{

    /// <summary>
    /// Handles <paramref name="message"/> and returns the result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result of handling <paramref name="message"/>.</returns>
    TResult Handle(TMessage message, ErgosfareContext context);
}
