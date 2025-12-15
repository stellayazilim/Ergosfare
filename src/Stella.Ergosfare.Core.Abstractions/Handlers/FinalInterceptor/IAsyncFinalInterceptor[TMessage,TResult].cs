using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an asynchronous final interceptor for a message pipeline, 
/// allowing custom logic to run after all other interceptors (pre, post, exception) have executed.
/// </summary>
/// <typeparam name="TMessage">The type of the message being intercepted. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the message handler. Must be non-nullable.</typeparam>
/// <remarks>
/// A final interceptor always executes at the end of the pipeline, regardless of whether
/// the message handling succeeded or an exception occurred.  
/// 
/// The <c>HandleAsync</c> method provides access to:
/// <list type="bullet">
///   <item><description>The original <typeparamref name="TMessage"/>.</description></item>
///   <item><description>The <typeparamref name="TResult"/> result, if any (nullable).</description></item>
///   <item><description>The exception thrown during message handling, if any.</description></item>
///   <item><description>The current execution context (<see cref="IExecutionContext"/>).</description></item>
/// </list>
/// 
/// Implementations cannot modify the result directly, but can perform logging, cleanup,
/// metrics collection, or other side effects.
/// </remarks>
public interface IAsyncFinalInterceptor<in TMessage, in TResult>: IFinalInterceptor<TMessage, TResult> 
    where TMessage : notnull
{
    /// <inheritdoc cref="IFinalInterceptor{TMessage, TResult}.Handle"/>
    object IFinalInterceptor<TMessage, TResult>.Handle(TMessage message, TResult? result, Exception? exception,
        IExecutionContext context)
    {
        return HandleAsync(message, result, exception, context);
    }

    /// <summary>
    /// Asynchronously handles a message at the end of the pipeline.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The result produced by the handler, if any. Can be null.</param>
    /// <param name="exception">The exception thrown during handling, if any.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(TMessage message, TResult? result, Exception? exception, IExecutionContext context);
}