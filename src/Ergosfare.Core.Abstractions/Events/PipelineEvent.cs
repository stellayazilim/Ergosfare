using System;
using System.Collections.Generic;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;


/// <summary>
/// Base class for all events in the pipeline that are published to the event hub.
/// Provides the message being processed and an optional result from the pipeline stage.
/// </summary>
public abstract class PipelineEvent: HubEvent
{
    
    /// <summary>
    /// Gets the message that triggered this pipeline event.
    /// This property is required and cannot be null.
    /// </summary>
    public required object Message { get; init; }
    
    /// <summary>
    /// Gets the result produced by the pipeline stage.
    /// This property is optional and can be null if no result was produced.
    /// </summary>
    public object? Result { get; init; }

    
    /// <summary>
    /// Subscribes a handler to receive events of the specified <typeparamref name="TEvent"/> type
    /// from the global <see cref="EventHubAccessor"/> instance.
    /// </summary>
    /// <typeparam name="TEvent">The type of pipeline event to subscribe to.</typeparam>
    /// <param name="handler">The action to invoke when the event is published.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that can be disposed to unsubscribe the handler.
    /// </returns>
    public static IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : PipelineEvent
    {
        return EventHubAccessor.Instance.Subscribe(handler);
    }

    
    /// <summary>
    /// Returns the components used to determine equality for this event.
    /// Includes <see cref="Message"/> and <see cref="Result"/> (or a new object if <c>null</c>).
    /// </summary>
    /// <returns>An enumerable of objects that represent the equality components.</returns>
    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Message;
        yield return Result ?? null!;
    }
}