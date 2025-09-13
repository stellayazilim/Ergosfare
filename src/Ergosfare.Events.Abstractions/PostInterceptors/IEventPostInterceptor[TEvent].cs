using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventPostInterceptor<in TEvent> : IEvent, IAsyncPostInterceptor<TEvent> where TEvent : notnull;