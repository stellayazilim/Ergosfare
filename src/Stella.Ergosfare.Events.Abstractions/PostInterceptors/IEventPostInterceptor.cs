
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Represents a non-generic post-interceptor for events, allowing custom logic
/// to execute after any event handlers have been invoked.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a non-generic version of <see cref="IEventPostInterceptor{TEvent}"/>,
/// applying to all events implementing <see cref="IEvent"/>.
/// </para>
/// <para>
/// It inherits from <see cref="IAsyncPostInterceptor{TMessage}"/>, enabling asynchronous
/// post-processing of events after they are dispatched to their handlers.
/// </para>
/// <para>
/// Event handlers and messages that implement <see cref="IEvent"/> will recognize
/// this interceptor automatically in the event mediation pipeline.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPostInterceptor : IEvent, IAsyncPostInterceptor<IEvent>
{
    
    /// <inheritdoc cref="IAsyncPostInterceptor{TEvent, Task}.HandleAsync"/>
    async Task<object> IAsyncPostInterceptor<IEvent>.HandleAsync(IEvent @event, object result, IExecutionContext context)
    {
        await HandleAsync(@event, result, context)!;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the event asynchronously after the main handlers have executed.
    /// </summary>
    /// <param name="event">The event being processed.</param>
    /// <param name="result">
    /// The result returned by the main handlers, or <c>null</c> if the event does not produce a result.
    /// </param>
    /// <param name="executionContext">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous post-processing operation.</returns>
    Task HandleAsync(IEvent @event, Task result, IExecutionContext executionContext);

}