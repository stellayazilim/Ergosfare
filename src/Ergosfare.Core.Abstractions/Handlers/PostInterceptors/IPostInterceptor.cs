namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a post-interceptor in the pipeline, which executes after the main handler has processed a message.
/// </summary>
/// <remarks>
/// The <see cref="Handle"/> method allows inspecting or modifying the result of a message after it has been handled.
/// The return type is <see cref="object"/> to remain type-agnostic and support pipelines where the concrete result type
/// may only be known at runtime. This enables both legacy and generic/fluent-result approaches without forcing the interceptor
/// to know the exact result type at compile time.
/// </remarks>
public interface IPostInterceptor
{
    
    /// <summary>
    /// Handles a message after it has been processed by the main handler.
    /// </summary>
    /// <param name="message">The message that was handled by the main handler.</param>
    /// <param name="messageResult">
    /// The result produced by the main handler. Interceptors can inspect, modify, or replace this result.
    /// </param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// Returns the (possibly modified) result as <see cref="object"/>. It is expected that the actual result matches the
    /// type produced by the main handler.
    /// </returns>
    object Handle(object message, object? messageResult, IExecutionContext context);
}