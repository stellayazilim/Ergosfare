
using Stella.Ergosfare.Core.Abstractions.Invokers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes final interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// Final interceptors are executed after pre-, post-, and exception interceptors. The
/// final interceptor list is pre-merged in registration order: direct final interceptors
/// first, then indirect ones. Final interceptors do not rethrow exceptions; they are
/// intended for cleanup, logging, or finalization logic.
/// </remarks>
internal sealed class TaskFinalInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService,
    IServiceProvider serviceProvider) : FinalInvoker(messageDependencies, resultAdapterService, serviceProvider)
{

    /// <summary>
    /// Executes all final interceptors (direct and indirect, pre-merged) for the specified message.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be passed to final interceptors.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation of executing all final interceptors.</returns>
    public override async Task Invoke(object message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        var interceptors = MessageDependencies.FinalInterceptors;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var handler = interceptors[i].Resolve(ServiceProvider);

            // Execute interceptor
            await (Task) handler.Handle(message, result, exception, executionContext);
        }
    }
}
