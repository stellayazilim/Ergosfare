using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventFinalInterceptor : IEvent, IAsyncFinalInterceptor<IEvent, object>;
    