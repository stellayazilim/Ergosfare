using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Represents a final interceptor for events, allowing custom logic
/// to be executed after all other event processing (handlers, pre-, post-interceptors)
/// has completed.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted. Must implement <see cref="IEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Implementing <see cref="IEventFinalInterceptor{TEvent}"/> allows the interceptor
/// to participate in the event mediation pipeline at the final stage, after
/// all handlers and other interceptors have run.
/// </para>
/// <para>
/// This interface inherits from <see cref="IAsyncFinalInterceptor{TEvent, TResult}"/>,
/// so final-interceptor logic can be asynchronous and return a <see cref="Task"/>.
/// </para>
/// <para>
/// The <typeparamref name="TEvent"/> type must implement <see cref="IEvent"/>.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventFinalInterceptor<in TEvent>:
    IEvent, IAsyncFinalInterceptor<TEvent, Task> where TEvent : IEvent;