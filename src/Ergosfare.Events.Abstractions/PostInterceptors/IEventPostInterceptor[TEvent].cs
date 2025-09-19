using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

/// <summary>
/// Represents a type-safe asynchronous post-interceptor for events, allowing custom logic
/// to execute after the event handlers have been invoked.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted. Must implement <see cref="IEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows post-processing logic to participate in the
/// event mediation pipeline after the main handlers have executed.
/// </para>
/// <para>
/// This interface inherits from <see cref="IAsyncPostInterceptor{TEvent}"/>,
/// so post-interceptor logic can be asynchronous.
/// </para>
/// <para>
/// The <typeparamref name="TEvent"/> type must be non-nullable and implement <see cref="IEvent"/>.
/// </para>
/// </remarks>
public interface IEventPostInterceptor<in TEvent> : IEvent, IAsyncPostInterceptor<TEvent> where TEvent : notnull
{
    /// <inheritdoc cref="IAsyncPostInterceptor{TEvent, Task}.HandleAsync"/>
    async Task<object> IAsyncPostInterceptor<TEvent>.HandleAsync(TEvent @event, object? result, IExecutionContext context)
    {
        return await HandleAsync(@event, result, context);
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
    Task HandleAsync(TEvent @event, Task? result, IExecutionContext executionContext);
}
