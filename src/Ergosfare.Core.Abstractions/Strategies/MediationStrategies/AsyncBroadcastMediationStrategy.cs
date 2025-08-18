using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Context;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Core.Abstractions.Strategies;

public sealed class AsyncBroadcastMediationStrategy<TMessage> : IMessageMediationStrategy<TMessage, Task> where TMessage : notnull
{
    private readonly EventMediationSettings _settings;

    public AsyncBroadcastMediationStrategy(EventMediationSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    ///     Mediates the given message by broadcasting it to all registered handlers concurrently.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="messageDependencies">
    ///     The dependencies required for message handling, including registered handlers,
    ///     pre-handlers, post-handlers, and error handlers.
    /// </param>
    /// <param name="executionContext"></param>
    /// <returns>A Task representing the asynchronous operation of the mediation process.</returns>
    public async Task Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext executionContext)
    {

        var handlers = messageDependencies.Handlers
            .Where(x => _settings.Filters.HandlerPredicate(x.Descriptor.HandlerType))
            .ToList();

        if (handlers.Count == 0)
        {
            if (_settings.ThrowIfNoHandlerFound)
            {
                throw new InvalidOperationException($"No handler found for message type '{typeof(TMessage)}'.");
            }
            return;
        }
        try
        {
            var sequentialExecutionTask = PublishSequentially(message, handlers);
            await Task.CompletedTask;
        }
        catch (Exception e) {  }
    }

    private static async Task PublishSequentially(TMessage message, IEnumerable<LazyHandler<IHandler, IMainHandlerDescriptor>> mainHandlers)
    {
        foreach (var lazyHandler in mainHandlers)
        {
            var handleTask = (Task) lazyHandler.Handler.Value.Handle(message);

            await handleTask;
        }
    }
}