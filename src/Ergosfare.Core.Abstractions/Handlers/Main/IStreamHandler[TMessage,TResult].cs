using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a handler that streams results asynchronously for messages of type <typeparamref name="TMessage"/>.
/// Produces an <see cref="IAsyncEnumerable{TResult}"/> to enable asynchronous streaming of multiple results.
/// </summary>
/// <typeparam name="TMessage">The type of the message to handle. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of each item in the streamed result. Must be non-nullable.</typeparam>
/// <remarks>
/// This interface extends <see cref="IHandler{TMessage, TResult}"/> with <typeparamref name="TResult"/> 
/// set to <see cref="IAsyncEnumerable{TResult}"/>, allowing the handler to produce multiple results asynchronously.
/// The explicit interface implementation maps the generic <see cref="IHandler{TMessage, TResult}.Handle"/> method
/// to the strongly-typed <see cref="StreamAsync"/> method.
/// </remarks>
public interface IStreamHandler<in TMessage, out TResult>
    :IHandler<TMessage, IAsyncEnumerable<TResult>>
        where TMessage : notnull
{
    /// <inheritdoc cref="IHandler{TMessage, TResult}.Handle"/>
    IAsyncEnumerable<TResult> IHandler<TMessage, IAsyncEnumerable<TResult>>.Handle(
        TMessage message, 
        IExecutionContext context)
    {
        return StreamAsync(message, context);
    }
    
    
    /// <summary>
    /// Streams results asynchronously for a given message.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{TResult}"/> representing the streamed asynchronous results of the message handling.
    /// </returns>
    IAsyncEnumerable<TResult> StreamAsync(TMessage message,IExecutionContext context);
}