using System;

namespace Ergosfare.Core.Abstractions.EventHub;

public interface IEventHub
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool useWeakReference = false)
        where TEvent : HubEvent;

    void Publish<TEvent>(TEvent evt) where TEvent : HubEvent;

    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : HubEvent;
    
}
