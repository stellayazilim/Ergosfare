using System;

namespace Ergosfare.Core.Abstractions.SignalHub;

public interface ISignalHub
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool useWeakReference = false)
        where TEvent : Signal;

    void Publish<TEvent>(TEvent evt) where TEvent : Signal;

    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : Signal;
    
}
