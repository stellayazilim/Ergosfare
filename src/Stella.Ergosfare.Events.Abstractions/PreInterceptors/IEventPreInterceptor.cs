using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Represents a non-generic pre-interceptor for events, allowing custom logic
/// to execute before any event handlers are invoked.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a non-generic version of <see cref="IEventPreInterceptor{TEvent}"/>,
/// applying to all events implementing <see cref="IEvent"/>.
/// </para>
/// <para>
/// It inherits from <see cref="IAsyncPreInterceptor{TMessage}"/>, enabling asynchronous
/// pre-processing of events before they are dispatched to their handlers.
/// </para>
/// <para>
/// Event handlers and messages that implement <see cref="IEvent"/> will recognize
/// this interceptor automatically in the event mediation pipeline.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPreInterceptor : IEvent, IAsyncPreInterceptor<IEvent>
{
    /// <inheritdoc cref="IAsyncPreInterceptor{TEvent}.HandleAsync"/>
    async ValueTask<object> IAsyncPreInterceptor<IEvent>.HandleAsync(IEvent @event, IExecutionContext executionContext)
    {
        await HandleAsync(@event, executionContext);
        return ValueTask.CompletedTask;
    }
    
    /// <summary>
    /// Handles the event asynchronously before the main handlers are invoked.
    /// </summary>
    /// <param name="event">The event to be processed.</param>
    /// <param name="executionContext">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous pre-processing operation.</returns>
    new ValueTask HandleAsync(IEvent @event, IExecutionContext executionContext);
}