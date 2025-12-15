
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Represents an asynchronous exception interceptor for events, allowing custom logic
/// to execute when an exception occurs during event handling.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted. Must be non-nullable and implement <see cref="IEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows the interceptor to participate in the event mediation
/// pipeline when an exception is thrown during the handling of <typeparamref name="TEvent"/>.
/// </para>
/// <para>
/// This interface inherits from <see cref="IAsyncExceptionInterceptor{TMessage}"/>, enabling
/// asynchronous exception handling logic.
/// </para>
/// <para>
/// The <typeparamref name="TEvent"/> type must be non-nullable and implement <see cref="IEvent"/>.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptor<in TEvent> : IEvent, IAsyncExceptionInterceptor<TEvent, Task>
    where TEvent : notnull
{
    
    /// <inheritdoc cref="IAsyncExceptionInterceptor{TEvent}.HandleAsync"/>
    async Task<object> IAsyncExceptionInterceptor<TEvent, Task>.HandleAsync(TEvent @event, Task? result,
        Exception exception, IExecutionContext context)
    {
        await HandleAsync(@event, result, exception, context);
        return Task.CompletedTask;
    }
    
    
    /// <summary>
    /// Handles an exception asynchronously that occurred during the processing of the event.
    /// </summary>
    /// <param name="event">The event being processed.</param>
    /// <param name="result">
    /// The result returned by the main handlers, or <c>null</c> if the event does not produce a result.
    /// </param>
    /// <param name="exception">The exception thrown during event handling.</param>
    /// <param name="context">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous exception handling operation.</returns>
    new Task HandleAsync(TEvent @event, Task? result, Exception exception, IExecutionContext context);
}


