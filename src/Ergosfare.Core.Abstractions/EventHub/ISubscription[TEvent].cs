using System;

namespace Ergosfare.Core.Abstractions.EventHub;


public interface ISubscription<TEvent> : IDisposable where TEvent : HubEvent
{
    bool Invoke(TEvent evt);
    bool IsAlive { get; }

    bool Matches(Action<TEvent> action);
}