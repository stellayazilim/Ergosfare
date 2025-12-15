namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a strongly-typed handler for processing messages of type <typeparamref name="TMessage"/>
/// and producing a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TMessage">The type of the message to handle. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the handler. Must be non-nullable.</typeparam>
/// <remarks>
/// Implementations of this interface should provide the synchronous handling logic for a message and return
/// a strongly typed result. The non-generic <see cref="IHandler"/> interface is implemented explicitly to allow
/// type-agnostic invocation, mapping the <paramref name="message"/> to <typeparamref name="TMessage"/> and returning
/// the typed <typeparamref name="TResult"/> as <see cref="object"/>.
/// </remarks>
public interface IHandler<in TMessage, out TResult> : IHandler
    where TMessage : notnull
    where TResult : notnull
{
    
    /// <inheritdoc cref="IHandler.Handle"/>
    object IHandler.Handle(object message, IExecutionContext context)
    {
        return Handle((TMessage) message, context);
    }
    
    /// <summary>
    /// Handles a message of type <typeparamref name="TMessage"/> and returns a strongly-typed result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>The result of type <typeparamref name="TResult"/> produced by handling the message.</returns>
    TResult Handle(TMessage message, IExecutionContext context);
}