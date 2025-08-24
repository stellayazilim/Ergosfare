using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventExceptionInterceptor: IEvent, IAsyncExceptionInterceptor<IEvent, object>;