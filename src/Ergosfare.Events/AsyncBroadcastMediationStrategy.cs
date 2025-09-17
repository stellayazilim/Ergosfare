using System.Runtime.ExceptionServices;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events;

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

    private async Task PublishSequentially(TMessage message, IEnumerable<ILazyHandler<IHandler, IMainHandlerDescriptor>> handlers, IExecutionContext context)
    {
        foreach (var lazyHandler in handlers)
        {
            var handleTask = (Task)lazyHandler.Handler.Value.Handle(message, context);

            await handleTask;
        }
    }
}