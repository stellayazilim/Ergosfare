
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Represents an asynchronous handler for messages of type <typeparamref name="TMessage"/>.
/// Produces a <see cref="ValueTask"/> as the result, allowing for asynchronous workflows.
/// </summary>
/// <typeparam name="TMessage">The type of the message to handle. Must be non-nullable.</typeparam>
/// <remarks>
/// This interface extends the generic <see cref="IHandler{TMessage, TResult}"/> with <typeparamref>
///     <name>TResult</name>
/// </typeparamref>
/// set to <see cref="ValueTask"/>, enabling asynchronous message processing.
/// Implementations that already hold a <see cref="Task"/> can wrap it allocation-free via
/// <c>new ValueTask(task)</c>; async method bodies work unchanged.
/// The explicit interface implementation maps the generic <see cref="IHandler{TMessage, TResult}.Handle"/> method
/// to the strongly-typed <see cref="HandleAsync"/> method.
/// </remarks>
public interface IAsyncHandler<in TMessage>: IHandler
    where TMessage : notnull
{
    /// <summary>
    /// Handles a message of type <typeparamref name="TMessage"/> asynchronously.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous handling operation.</returns>
    ValueTask HandleAsync(TMessage message, IExecutionContext context);
}
