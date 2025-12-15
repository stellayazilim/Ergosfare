using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents an exception interceptor for commands, allowing modification of the result type
/// when an exception occurs during pipeline execution.
/// </summary>
/// <typeparam name="TCommand">The type of command being handled. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The original result type of the command. Must be non-nullable.</typeparam>
/// <typeparam name="TModifiedResult">
/// The modified result type returned by this interceptor. Must be compatible with <typeparamref name="TResult"/>.
/// </typeparam>
/// <remarks>
/// This interface is a type-safe extension of <see cref="IAsyncExceptionInterceptor{TCommand,TResult}"/>,
/// allowing an exception interceptor to produce a modified result that continues through the pipeline.
/// 
/// The <c>HandleAsync</c> method is invoked only if an exception occurs for the associated command.
/// 
/// For non-type-safe or generic usage, consider using <see cref="IAsyncExceptionInterceptor{TCommand,TResult}"/> directly.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptor<in TCommand,in TResult, TModifiedResult>: 
    IAsyncExceptionInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
    where TModifiedResult:  TResult
{
    /// <inheritdoc/>
    async Task<object?> IAsyncExceptionInterceptor<TCommand, TResult>
        .HandleAsync(TCommand command, TResult? result,
        Exception? exception, IExecutionContext context)
    {
        return await HandleAsync(command, result, exception, context);
    }

    
    /// <summary>
    /// Handles the exception asynchronously, potentially modifying the command result.
    /// </summary>
    /// <param name="command">The command being processed.</param>
    /// <param name="result">The original result produced before the exception occurred. May be null.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation, producing a potentially modified result 
    /// of type <typeparamref name="TModifiedResult"/> to continue through the pipeline.
    /// </returns>
    new Task<TModifiedResult?> HandleAsync(TCommand command, TResult? result, Exception? exception, IExecutionContext context);
}