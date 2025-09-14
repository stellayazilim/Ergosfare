using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventFinalInterceptor<in TEvent>:
    IEvent, IAsyncFinalInterceptor<TEvent> where TEvent : IEvent;