using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Events.Abstractions;

public interface IEventHandler<in TEvent>: IEvent, IAsyncHandler<TEvent> where TEvent : notnull;