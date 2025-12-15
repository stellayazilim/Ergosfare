using System.Runtime.ExceptionServices;

namespace Stella.Ergosfare.Core.Abstractions.Invokers;

/// <summary>
/// Provides an abstract base for invoking exception interceptors within the message pipeline.
/// </summary>
/// <remarks>
/// Exception interceptors run when an exception occurs during message processing. 
/// They can inspect, modify, or handle the exception. If no interceptors exist,
/// the captured exception is rethrown. This makes <see cref="ExceptionInvoker"/> 
/// responsible for exception flow, unlike <see cref="FinalInvoker"/> which only observes exceptions.
/// </remarks>
internal abstract class ExceptionInvoker(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService) 
    : AbstractInvoker(messageDependencies,resultAdapterService)
{
    /// <summary>
    /// Invokes exception interceptors for the given <paramref name="message"/> 
    /// and <paramref name="result"/>, using the captured <paramref name="exceptionDispatchInfo"/>.
    /// </summary>
    /// <param name="message">The message instance being processed.</param>
    /// <param name="result">The result produced before the exception occurred, if any.</param>
    /// <param name="exceptionDispatchInfo">
    /// The captured exception information. If no interceptors exist, this exception is rethrown.
    /// </param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// An object representing the outcome after all exception interceptors have run. 
    /// This may be a transformed result or the original result if no interceptor modified it.
    /// </returns>
    public abstract object Invoke(
        object message, 
        object result, 
        ExceptionDispatchInfo exceptionDispatchInfo, 
        IExecutionContext executionContext);
}