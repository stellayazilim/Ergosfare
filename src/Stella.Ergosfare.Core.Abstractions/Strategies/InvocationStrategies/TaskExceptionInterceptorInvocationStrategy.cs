using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Invokers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes exception interceptors for a message using <see cref="Task"/>-based handlers.
/// </summary>
/// <remarks>
/// The exception interceptor list is pre-merged in registration order: direct exception
/// interceptors first, then indirect ones. If no exception interceptors are registered,
/// the captured exception is rethrown immediately.
/// </remarks>
internal sealed class TaskExceptionInterceptorInvocationStrategy(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService,
    IServiceProvider serviceProvider) : ExceptionInvoker(messageDependencies, resultAdapterService, serviceProvider)
{

    /// <summary>
    /// Executes all exception interceptors (direct and indirect, pre-merged) for the specified message and result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message handling, which may be transformed by exception interceptors.</param>
    /// <param name="exceptionDispatchInfo">The captured exception information to be passed to interceptors.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The task result contains the transformed result after all exception interceptors have executed.
    /// If no interceptors exist, the captured exception is rethrown.
    /// </returns>
    public override async Task<object?> Invoke(object message, object? result, ExceptionDispatchInfo exceptionDispatchInfo,
        IExecutionContext executionContext)
    {
        if (ExceptionInterceptorCount == 0) exceptionDispatchInfo.Throw();

        var interceptors = MessageDependencies.ExceptionInterceptors;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var handler = interceptors[i].Resolve(ServiceProvider);
            var objectResult = handler.Handle(message, result, exceptionDispatchInfo.SourceException, executionContext);
            result = await ConvertTask(objectResult);
        }

        return result;
    }
}
