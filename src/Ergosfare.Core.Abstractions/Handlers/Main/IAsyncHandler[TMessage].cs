using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Represents an asynchronous handler for messages of type <typeparamref name="TMessage"/>.
/// Produces a <see cref="Task"/> as the result, allowing for asynchronous workflows.
/// </summary>
/// <typeparam name="TMessage">The type of the message to handle. Must be non-nullable.</typeparam>
/// <remarks>
/// This interface extends the generic <see cref="IHandler{TMessage, TResult}"/> with <typeparamref name="TResult"/> 
/// set to <see cref="Task"/>, enabling asynchronous message processing. 
/// The explicit interface implementation maps the generic <see cref="IHandler{TMessage, TResult}.Handle"/> method
/// to the strongly-typed <see cref="HandleAsync"/> method.
/// </remarks>
public interface IAsyncHandler<in TMessage>: IHandler<TMessage, Task>
    where TMessage : notnull
{
    /// <inheritdoc cref="IHandler{TMessage, TResult}.Handle"/>
    Task IHandler<TMessage, Task>.Handle(TMessage message, IExecutionContext context)
    {
        return HandleAsync(message, context);
    }
        
    /// <summary>
    /// Handles a message of type <typeparamref name="TMessage"/> asynchronously.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous handling operation.</returns>   
    Task HandleAsync(TMessage message, IExecutionContext context);
}



