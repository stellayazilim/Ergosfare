using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Invokers;

/// <summary>
/// Provides an abstract base for invoking main handlers within the message pipeline.
/// </summary>
/// <remarks>
/// Main handlers are responsible for processing the primary logic of a message.
/// They execute after pre-interceptors and before post-, exception-, or final interceptors.
/// </remarks>
internal abstract class MainInvoker(
    IMessageDependencies messageDependencies,
    IResultAdapterService resultAdapterService) 
    : AbstractInvoker(messageDependencies, resultAdapterService)
{
    /// <summary>
    /// Invokes the main handlers for the specified <paramref name="message"/> 
    /// using the provided <paramref name="executionContext"/>.
    /// </summary>
    /// <param name="message">The message instance being processed.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// An object representing the result of the main handler execution. 
    /// This result may later be transformed by post- or exception interceptors.
    /// </returns>
    public abstract object Invoke(object message, IExecutionContext executionContext);
}