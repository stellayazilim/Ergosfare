using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventExceptionInterceptor<in TEvent> : IEvent, IAsyncExceptionInterceptor<TEvent, object> where TEvent: notnull;
