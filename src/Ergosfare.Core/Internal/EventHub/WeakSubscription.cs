using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Internal.EventHub;


internal sealed class WeakSubscription<TEvent> : ISubscription<TEvent> where TEvent : HubEvent
{
    private readonly WeakReference<Action<TEvent>> _weak;
    public WeakSubscription(Action<TEvent> action) => _weak = new(action);
    public bool Invoke(TEvent evt)
    {
        if (_weak.TryGetTarget(out var target))
        {
            target(evt);
            return true;
        }
        return false;
    }
    

    public bool Matches(Action<TEvent> action)
    {
        return _weak.TryGetTarget(out var target) && target.Equals(action);
    }


    public bool IsAlive => _weak.TryGetTarget(out _);
    
 

    public void Dispose() { /* nothing to dispose */ }
}