using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventExceptionInterceptor: IEvent, IAsyncExceptionInterceptor<IEvent, object>;