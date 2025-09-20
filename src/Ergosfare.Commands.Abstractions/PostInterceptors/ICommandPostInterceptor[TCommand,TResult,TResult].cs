using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a type-safe, asynchronous post-interceptor for a command pipeline.
/// This interceptor executes after the command handler has completed,
/// and can modify the result before it propagates further in the pipeline.
/// </summary>
/// <typeparam name="TCommand">
/// The type of command being handled. Must implement <see cref="ICommand{TResult}"/>.
/// </typeparam>
/// <typeparam name="TResult">
/// The original result type produced by the command handler.
/// </typeparam>
/// <typeparam name="TModifiedResult">
/// The result type that this interceptor can return. Must be compatible with <typeparamref name="TResult"/>.
/// Nullable to allow returning null if appropriate.
/// </typeparam>
public interface ICommandPostInterceptor<in TCommand, in TResult,  TModifiedResult> : ICommandPostInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
    where TModifiedResult : TResult
{

    /// <summary>
    /// Explicit interface implementation for type-erased asynchronous handling.
    /// Converts the type-safe result into <see cref="object"/> for base interface compatibility.
    /// </summary>
    /// <param name="command">The command being handled.</param>
    /// <param name="result">The result from the command handler. May be null.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>
    /// A <see cref="Task{object}"/> representing the asynchronous operation.
    /// Returns the modified result boxed as <see cref="object"/>.
    /// </returns>
    async Task<object> IAsyncPostInterceptor<TCommand, TResult>.HandleAsync(TCommand command, TResult? result,
        IExecutionContext context)
    {
        return (await HandleAsync(command, result, context))!;
    }
    
    
    /// <summary>
    /// Handles the post-processing of a command asynchronously.
    /// </summary>
    /// <param name="command">The command that was executed.</param>
    /// <param name="commandResult">The result produced by the command handler. May be null.</param>
    /// <param name="context">The execution context providing metadata and cancellation support.</param>
    /// <returns>
    /// A <see cref="Task{T}"/> representing the asynchronous operation.
    /// The task result is the potentially modified result of type <typeparamref name="TModifiedResult"/> or null.
    /// </returns>
    new Task<TModifiedResult?> HandleAsync(TCommand command, TResult? commandResult, IExecutionContext context);
}
