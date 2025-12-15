using System;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a final interceptor in the message pipeline that operates on untyped messages and results.
/// </summary>
/// <remarks>
/// Final interceptors always execute at the end of the pipeline, regardless of whether the message handling
/// succeeded or an exception occurred. This interface is non-generic and works with <see cref="object"/> types,
/// making it suitable for internal or heterogeneous message handling scenarios.
/// 
/// Implementations can perform logging, metrics collection, cleanup, or other side effects.
/// They should not attempt to modify the result directly.
/// </remarks>
public interface IFinalInterceptor
{
    /// <summary>
    /// Handles a message at the end of the pipeline.
    /// </summary>
    /// <param name="message">The original message being processed.</param>
    /// <param name="result">The result produced by the handler, if any. May be null.</param>
    /// <param name="exception">The exception thrown during message handling, if any. May be null.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>An <see cref="object"/> representing the outcome of the final interceptor. 
    /// Typically, this is ignored since final interceptors do not modify results.</returns>
    object Handle(object message, object? result, Exception? exception, IExecutionContext context);
}