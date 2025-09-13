using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventExceptionInterceptor<in TEvent> : IEvent, IAsyncExceptionInterceptor<TEvent, object> where TEvent: notnull;
