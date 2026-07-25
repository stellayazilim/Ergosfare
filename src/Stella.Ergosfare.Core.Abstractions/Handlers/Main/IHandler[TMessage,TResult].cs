namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a strongly-typed handler for processing messages of type <typeparamref name="TMessage"/>
/// and producing a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TMessage">The type of the message to handle. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the handler. Must be non-nullable.</typeparam>
/// <remarks>
/// Implementations of this interface provide synchronous handling logic and return the
/// typed result directly — there is no object-typed bridge member; the pipeline invokes
/// handlers exclusively through their typed members.
/// </remarks>
public interface IHandler<in TMessage, out TResult> : IHandler
    where TMessage : notnull
    where TResult : notnull
{

    /// <summary>
    /// Handles a message of type <typeparamref name="TMessage"/> and returns a strongly-typed result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>The result of type <typeparamref name="TResult"/> produced by handling the message.</returns>
    TResult Handle(TMessage message, IExecutionContext context);
}