using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventPreInterceptor<in TEvent>: IEvent, IAsyncPreInterceptor<TEvent> where TEvent : notnull;