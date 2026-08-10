using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

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
/// This interface inherits from <see cref="IAsyncPostInterceptor{TMessage}"/>,
/// so post-interceptor logic can be asynchronous.
/// </para>
/// <para>
/// The <typeparamref name="TEvent"/> type must be non-nullable and implement <see cref="IEvent"/>.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPostInterceptor<in TEvent> : IEvent, IAsyncPostInterceptor<TEvent> where TEvent : notnull
{
    /// <inheritdoc cref="IAsyncPostInterceptor{TEvent}.HandleAsync"/>
    /// <remarks>
    /// A publish has no result: the slot carries <see cref="Unit.Value"/> and is not
    /// forwarded. The typed member below keeps its <see cref="ValueTask"/> parameter — it
    /// only ever received the completed task — and gets it directly, so nothing is cast
    /// out of the slot and nothing is boxed back into it.
    /// </remarks>
    async ValueTask<object> IAsyncPostInterceptor<TEvent>.HandleAsync(TEvent @event, object result, IExecutionContext context)
    {
        // The ValueTask argument is what binds this call to the typed member below: an
        // object-typed one resolves back to the inherited interface member — this very
        // implementation — and recurses infinitely.
        await HandleAsync(@event, ValueTask.CompletedTask, context);
        return Unit.Value;
    }
    
    
    /// <summary>
    /// Handles the event asynchronously after the main handlers have executed.
    /// </summary>
    /// <param name="event">The event being processed.</param>
    /// <param name="result">
    /// The result returned by the main handlers, or <c>null</c> if the event does not produce a result.
    /// </param>
    /// <param name="executionContext">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous post-processing operation.</returns>
    ValueTask HandleAsync(TEvent @event, ValueTask result, IExecutionContext executionContext);
}
