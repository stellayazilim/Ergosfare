using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Represents a type-safe pre-interceptor for events, allowing custom logic
/// to execute before the event handlers are invoked.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted. Must implement <see cref="IEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows pre-processing logic to participate in the
/// event mediation pipeline before the main handlers have executed.
/// </para>
/// <para>
/// This interface inherits from <see cref="IAsyncPreInterceptor{TEvent}"/>,
/// enabling asynchronous pre-processing of events.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPreInterceptor<in TEvent> : IEvent, IAsyncPreInterceptor<TEvent> where TEvent : notnull;