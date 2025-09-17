using System;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Invokers;

/// <summary>
/// Provides an abstract base for invoking final interceptors within the message pipeline.
/// </summary>
/// <remarks>
/// Final interceptors run at the very end of the pipeline, after pre-, post-, and exception interceptors.
/// They are intended for cleanup, logging, or finalization tasks, and do not rethrow exceptions.
/// </remarks>
internal abstract class FinalInvoker(
    IMessageDependencies messageDependencies, 
    IResultAdapterService? resultAdapterService) 
    : AbstractInvoker(messageDependencies, resultAdapterService)
{
    /// <summary>
    /// Invokes final interceptors for the given <paramref name="message"/>, 
    /// providing the <paramref name="result"/> (if any), the captured 
    /// <paramref name="exception"/> (if an error occurred), and the active 
    /// <paramref name="executionContext"/>.
    /// </summary>
    /// <param name="message">The message instance being processed.</param>
    /// <param name="result">The result produced during processing, if available.</param>
    /// <param name="exception">The exception that occurred during execution, if any; otherwise <c>null</c>.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// An object representing the outcome of the invocation. 
    /// The actual type depends on the handler and may be a result, task, or value task.
    /// </returns>
    public abstract object Invoke(
        object message, 
        object? result, 
        Exception? exception, 
        IExecutionContext executionContext);
}