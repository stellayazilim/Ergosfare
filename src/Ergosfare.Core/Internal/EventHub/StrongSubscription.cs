
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Internal.EventHub;

internal sealed class StrongSubscription<TEvent> : ISubscription<TEvent> where TEvent : HubEvent
{
    private readonly Action<TEvent> _action;
    public StrongSubscription(Action<TEvent> action) => _action = action;
    public bool Invoke(TEvent evt)
    {
        _action(evt);
        return true;
    }
    public bool IsAlive => true;
  
    public bool Matches(Action<TEvent> action)
    {
        return _action.Equals(action);
    }

    public void Dispose() { /* nothing to dispose */ }
}
