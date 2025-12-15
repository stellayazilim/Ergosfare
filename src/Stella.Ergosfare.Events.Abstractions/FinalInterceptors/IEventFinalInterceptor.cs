using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Represents a non-generic final interceptor for events, allowing custom logic
/// to execute after all event handlers and other interceptors have completed.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a non-generic version of <see cref="IEventFinalInterceptor{TEvent}"/>,
/// applying to all events implementing <see cref="IEvent"/>.
/// </para>
/// <para>
/// It inherits from <see cref="IAsyncFinalInterceptor{TMessage}"/>,
/// enabling asynchronous final processing of events after they are dispatched to their handlers.
/// </para>
/// <para>
/// Event handlers and messages that implement <see cref="IEvent"/> will recognize
/// this interceptor automatically in the event mediation pipeline.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventFinalInterceptor : IEvent, IAsyncFinalInterceptor<IEvent, Task>;
    