using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

/// <summary>
/// Represents a non-generic exception interceptor for events, allowing custom logic
/// to execute when an exception occurs during the handling of any <see cref="IEvent"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a non-generic version of <see cref="IEventExceptionInterceptor{TEvent}"/>,
/// applying to all events implementing <see cref="IEvent"/>.
/// </para>
/// <para>
/// It inherits from <see cref="IAsyncExceptionInterceptor{TEvent, TResult}"/>, enabling
/// asynchronous exception handling after event handlers have been invoked.
/// </para>
/// <para>
/// Event handlers and messages that implement <see cref="IEvent"/> will recognize
/// this interceptor automatically in the event mediation pipeline.
/// </para>
/// </remarks>
public interface IEventExceptionInterceptor: IEvent, IAsyncExceptionInterceptor<IEvent, Task>;