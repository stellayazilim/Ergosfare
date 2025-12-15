using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// Represents a mediation strategy that broadcasts a message asynchronously
/// to all registered handlers of the specified <typeparamref name="TMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The type of message being mediated.</typeparam>
/// <remarks>
/// <para>
/// This strategy ensures that:
/// <list type="bullet">
///   <item><description>All handlers for the message are invoked sequentially.</description></item>
///   <item><description>Pre-, post-, exception-, and final-interceptors are executed in the correct order.</description></item>
///   <item><description>Exceptions are captured and passed to the configured exception interceptors.</description></item>
/// </list>
/// </para>
/// <para>
/// Since this strategy is intended for event broadcasting, results are not adapted
/// (unlike request/response message patterns).
/// </para>
/// </remarks>
public sealed class AsyncBroadcastMediationStrategy<TMessage>(
    IResultAdapterService? resultAdapterService,
    EventMediationSettings settings)
    : IMessageMediationStrategy<TMessage, Task>
    where TMessage : notnull
{
    /// <summary>
    ///     Mediates the given message by broadcasting it to all registered handlers concurrently.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="messageDependencies">
    ///     The dependencies required for message handling, including registered handlers,
    ///     pre-handlers, post-handlers, and error handlers.
    /// </param>
    /// <param name="context"></param>
    /// <returns>A Task representing the asynchronous operation of the mediation process.</returns>
    public async Task Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context)
    {
        
        var executionTaskOfAllHandlers = Task.CompletedTask;
        var handlers = messageDependencies.Handlers
            .Where(x => settings.Filters.HandlerPredicate(x.Descriptor.HandlerType))
            .ToList();

        if (handlers.Count == 0)
        {
            if (settings.ThrowIfNoHandlerFound)
            {
                throw new NoHandlerFoundException(typeof(TMessage));
            }
            return;
        }
        Exception? exception = null;
        try
        {
            // events doesn't need result adapter, since events intended to not return a result
            var preInvoker = new TaskPreInterceptorInvocationStrategy(messageDependencies, null);
            await preInvoker.Invoke(message, context);
            var sequentialExecutionTask = PublishSequentially(message, handlers, context);
            await sequentialExecutionTask;

            var postInvoker = new TaskPostInterceptorInvocationStrategy(messageDependencies, null);
            await postInvoker.Invoke(message, sequentialExecutionTask, context);
        }
        catch (Exception e)
        {
            exception = e;
            var exceptionInvoker = new TaskExceptionInterceptorInvocationStrategy(messageDependencies, null);
            await exceptionInvoker.Invoke(message, executionTaskOfAllHandlers,
                ExceptionDispatchInfo.Capture(e), context);

        }

        finally
        {
            var finalInvoker = new TaskFinalInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            await finalInvoker.Invoke(message, executionTaskOfAllHandlers, exception, context);
        }
    }

    
    /// <summary>
    /// Publishes the message sequentially to all resolved handlers.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="handlers">The collection of handlers resolved for this message.</param>
    /// <param name="context">The execution context for this mediation pipeline.</param>
    private async Task PublishSequentially(TMessage message, IEnumerable<ILazyHandler<IHandler, IMainHandlerDescriptor>> handlers, IExecutionContext context)
    {
        foreach (var lazyHandler in handlers)
        {
            var handleTask = (Task)lazyHandler.Handler.Value.Handle(message, context);

            await handleTask;
        }
    }
}