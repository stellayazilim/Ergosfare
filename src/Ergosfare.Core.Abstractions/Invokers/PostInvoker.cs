namespace Ergosfare.Core.Abstractions.Invokers;

/// <summary>
/// Provides an abstract base for invoking post-interceptors after a message
/// handler has executed.
/// </summary>
internal abstract class PostInvoker(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService): 
    AbstractInvoker(messageDependencies, resultAdapterService)
{
    /// <summary>
    /// Executes the post-interceptors for the given message and result within
    /// the specified execution context.
    /// </summary>
    /// <param name="message">The original message being processed.</param>
    /// <param name="result">The result produced by the message handler.</param>
    /// <param name="executionContext">
    /// The execution context containing runtime information for invocation.
    /// </param>
    /// <returns>
    /// The transformed result after all post-interceptors have been executed.
    /// </returns>
    public abstract object Invoke(object message, object? result, IExecutionContext executionContext);
}