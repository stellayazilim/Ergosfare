namespace Ergosfare.Core.Internal.EventHub;

public sealed class StrongSubscription<TEvent>(Action<TEvent> action) : ISubscription<TEvent>
{
    public bool Invoke(TEvent evt)
    {
        action(evt);
        return true;
    }

    public bool IsAlive => true;
    public void Dispose() { /* nothing */ }
}