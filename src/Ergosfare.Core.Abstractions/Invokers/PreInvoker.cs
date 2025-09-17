using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Invokers;


internal abstract class PreInvoker(IMessageDependencies messageDependencies): AbstractInvoker(messageDependencies)
{
   
    /// <summary>
    /// Executes the pre-interceptors for the given message within the specified
    /// execution context.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="executionContext">
    /// The execution context containing runtime information for invocation.
    /// </param>
    /// <returns>
    /// An object representing the result of pre-interceptor execution, which
    /// may include a modified message or additional processing state.
    /// </returns>
    public abstract object Invoke(object message, IExecutionContext executionContext);
}