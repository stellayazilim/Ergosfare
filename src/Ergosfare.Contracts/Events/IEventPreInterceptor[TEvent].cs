using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventPreInterceptor<in TEvent>: IEvent, IAsyncPreInterceptor<TEvent> where TEvent : notnull;