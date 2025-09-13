using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventPreInterceptor: IEvent, IAsyncPreInterceptor<IEvent>;