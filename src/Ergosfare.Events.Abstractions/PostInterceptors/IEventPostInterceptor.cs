using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventPostInterceptor:  IEvent, IAsyncPostInterceptor<IEvent>;