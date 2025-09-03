using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Internal.EventHub;



public interface ISubscription<TEvent> : IDisposable where TEvent : HubEvent
{
    bool Invoke(TEvent evt);
    bool IsAlive { get; }

    bool Matches(Action<TEvent> action);
}