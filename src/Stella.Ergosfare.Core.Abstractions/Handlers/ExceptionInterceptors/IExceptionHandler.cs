using System;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an interceptor that handles exceptions thrown during message processing in the pipeline.
/// </summary>
/// <remarks>
/// Implementations of this interface can inspect the exception, modify it, or produce a new result
/// to continue through the pipeline. The returned <see cref="object"/> is the result that will
/// be passed to subsequent interceptors or handlers.
///
/// This design supports both legacy and fluent-result styles, making it flexible for pipelines
/// where the concrete result type may only be known at runtime.
/// </remarks>
public interface IExceptionInterceptor
{
    /// <summary>
    /// Handles an exception that occurred while processing a message.
    /// </summary>
    /// <param name="message">The message that was being handled when the exception occurred.</param>
    /// <param name="messageResult">The current result of the message, if any. Can be modified or replaced.</param>
    /// <param name="exception">The exception that was thrown during message handling.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// Returns the result of the message after handling the exception. This result will continue
    /// through the pipeline and can be modified by subsequent interceptors or handlers.
    /// </returns>
    object? Handle(object message,  object? messageResult, Exception exception,  IExecutionContext context);
}