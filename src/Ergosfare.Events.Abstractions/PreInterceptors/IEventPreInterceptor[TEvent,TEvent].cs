using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;


/// <summary>
/// Represents a type-safe pre-interceptor for events that can optionally modify
/// the event before it reaches its handlers.
/// </summary>
/// <typeparam name="TEvent">The type of the original event being intercepted. Must implement <see cref="IEvent"/>.</typeparam>
/// <typeparam name="TModifiedEvent">
/// The type of event returned after pre-processing. Must be the same or derived from <typeparamref name="TEvent"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows pre-processing logic to run before the event
/// is dispatched to its handlers and optionally return a modified version of the event.
/// </para>
/// <para>
/// This interface inherits from <see cref="IAsyncPreInterceptor{TEvent}"/>, enabling
/// asynchronous pre-processing in the event mediation pipeline.
/// </para>
/// </remarks>
public interface IEventPreInterceptor<in TEvent,  TModifiedEvent>: IAsyncPreInterceptor<TEvent>
    where TEvent : notnull
    where TModifiedEvent : TEvent
{
    /// <inheritdoc cref="IAsyncPreInterceptor{TEvent}.HandleAsync"/>
    async Task<object> IAsyncPreInterceptor<TEvent>.HandleAsync(TEvent @event, IExecutionContext executionContext)
    {
        return await HandleAsync(@event, executionContext);
    }
    
    /// <summary>
    /// Represents a type-safe pre-interceptor for events that can optionally modify
    /// the event before it reaches its handlers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the original event being intercepted. Must implement <see cref="IEvent"/>.</typeparam>
    /// <typeparam name="TModifiedEvent">
    /// The type of event returned after pre-processing. Must be the same or derived from <typeparamref name="TEvent"/>.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Implementing this interface allows pre-processing logic to run before the event
    /// is dispatched to its handlers and optionally return a modified version of the event.
    /// </para>
    /// <para>
    /// This interface inherits from <see cref="IAsyncPreInterceptor{TEvent}"/>, enabling
    /// asynchronous pre-processing in the event mediation pipeline.
    /// </para>
    /// </remarks>
    new Task<TModifiedEvent> HandleAsync(TEvent @event, IExecutionContext context);
}