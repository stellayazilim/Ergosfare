using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventHandler<in TEvent>: IEvent, IAsyncHandler<TEvent> where TEvent : notnull;