
using Stella.Ergosfare.Core.Abstractions.Invokers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes post-interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// The post-interceptor list is pre-merged in registration order: direct post-interceptors
/// first, then indirect ones. Each interceptor may transform the result; the final
/// returned result reflects all applied transformations.
/// </remarks>
internal sealed class TaskPostInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService,
    IServiceProvider serviceProvider) :
    PostInvoker(messageDependencies, resultAdapterService, serviceProvider)
{

    /// <summary>
    /// Executes all post-interceptors (direct and indirect, pre-merged) for the specified message and result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by post-interceptors.</param>
    /// <param name="context">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all post-interceptors have executed.
    /// </returns>
    public override async Task<object?> Invoke(object message, object? result,  IExecutionContext context)
    {
        var interceptors = MessageDependencies.PostInterceptors;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var handler = interceptors[i].Resolve(ServiceProvider);

            // Execute interceptor handler and await result
            result = await (Task<object?>) handler.Handle(message, result!, context);

            var ex = ResultAdapterService?.LookupException(result);

            if (ex != null) throw ex;
        }

        return result;
    }
}
