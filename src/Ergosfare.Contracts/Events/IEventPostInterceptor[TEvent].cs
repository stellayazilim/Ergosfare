using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventPostInterceptor<in TEvent> : IEvent, IAsyncPostInterceptor<TEvent> where TEvent : notnull;