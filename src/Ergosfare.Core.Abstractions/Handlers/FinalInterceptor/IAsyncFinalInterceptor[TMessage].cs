using System;
using System.Threading.Tasks;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an asynchronous final interceptor for a message pipeline that does not require a strongly-typed result.
/// </summary>
/// <typeparam name="TMessage">The type of the message being intercepted. Must be non-nullable.</typeparam>
/// <remarks>
/// A final interceptor always executes at the end of the pipeline, regardless of whether
/// the message handling succeeded or an exception occurred.
/// 
/// The <c>HandleAsync</c> method provides access to:
/// <list type="bullet">
///   <item><description>The original <typeparamref name="TMessage"/>.</description></item>
///   <item><description>The result produced by the handler, if any. Treated as <see cref="object"/> and may be null.</description></item>
///   <item><description>The exception thrown during message handling, if any.</description></item>
///   <item><description>The current execution context (<see cref="IExecutionContext"/>).</description></item>
/// </list>
/// 
/// Implementations cannot modify the result directly, but can perform logging, cleanup,
/// metrics collection, or other side effects.
/// 
/// This version is useful for internal pipeline scenarios where the result type is unknown or heterogeneous.
/// For strongly-typed results, consider using <see cref="IAsyncFinalInterceptor{TMessage,TResult}"/>.
/// </remarks>
public interface IAsyncFinalInterceptor<in TMessage>: IFinalInterceptor<TMessage, object>
{
    
    /// <inheritdoc cref="IFinalInterceptor{TMessage, TResult}"/>
    object IFinalInterceptor<TMessage, object>.Handle(TMessage message, object? result, Exception? exception,
        IExecutionContext context)
    {
        return HandleAsync((TMessage) message, result, exception, context);
    }
    
    
    /// <summary>
    /// Asynchronously handles a message at the end of the pipeline.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The result produced by the handler, if any. Treated as <see cref="object"/> and may be null.</param>
    /// <param name="exception">The exception thrown during handling, if any.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(TMessage message, object? result, Exception? exception, IExecutionContext context);
}