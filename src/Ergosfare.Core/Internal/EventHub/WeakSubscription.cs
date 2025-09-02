namespace Ergosfare.Core.Internal.EventHub;


public sealed class WeakSubscription<TEvent>(Action<TEvent> action) : ISubscription<TEvent>
{
    private readonly WeakReference<Action<TEvent>> _weak = new(action);

    public bool Invoke(TEvent evt)
    {
        if (_weak.TryGetTarget(out var target))
        {
            target(evt);
            return true;
        }
        return false;
    }

    public bool IsAlive => _weak.TryGetTarget(out _);
    public void Dispose() { /* nothing */ }
}