
using Stella.Ergosfare.Core.Abstractions.Invokers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;



/// <summary>
/// Executes pre-interceptors for a message using <see cref="ValueTask"/>-based handlers.
/// </summary>
/// <remarks>
/// The pre-interceptor list is pre-merged in registration order: direct pre-interceptors
/// first, then indirect ones. Each interceptor may transform the message; the final
/// returned message reflects all applied transformations.
/// </remarks>
internal sealed class TaskPreInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService,
    IServiceProvider serviceProvider) : PreInvoker(messageDependencies, resultAdapterService, serviceProvider)

{
    /// <summary>
    /// Executes all pre-interceptors (direct and indirect, pre-merged) for the specified message.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="ValueTask{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed message after all pre-interceptors have executed.
    /// </returns>
    public override async ValueTask<object> Invoke(object message, IExecutionContext executionContext)
    {
        var interceptors = MessageDependencies.PreInterceptors;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var handler = interceptors[i].Resolve(ServiceProvider);

            // Execute interceptor handler and await result
            message = await (ValueTask<object>) handler.Handle(message, executionContext);
        }

        return message;
    }
}
