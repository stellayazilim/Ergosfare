using System;

namespace Ergosfare.Core.Abstractions.EventHub;

public interface IEventHub
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool useWeakReference = false);
    void Publish<TEvent>(TEvent evt);
}
