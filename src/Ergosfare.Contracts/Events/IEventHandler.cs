namespace Ergosfare.Contracts;

public interface IEventHandler<in TEvent>: IEvent, IAsyncHandler<TEvent> where TEvent : notnull;