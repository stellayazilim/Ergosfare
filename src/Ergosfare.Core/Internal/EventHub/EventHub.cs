using System.Collections.Concurrent;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Internal.EventHub;

public class EventHub: IEventHub
{
    private readonly ConcurrentDictionary<Type, List<object>> _subscriptions = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool useWeakReference = false)
    {
        ISubscription<TEvent> subscription = useWeakReference
            ? new WeakSubscription<TEvent>(handler)
            : new StrongSubscription<TEvent>(handler);

        var subs = _subscriptions.GetOrAdd(typeof(TEvent), _ => new List<object>());
        lock (subs)
        {
            subs.Add(subscription);
        }

        return subscription;
    }

    public void Publish<TEvent>(TEvent evt)
    {
        if (!_subscriptions.TryGetValue(typeof(TEvent), out var subs))
            return;

        List<ISubscription<TEvent>> typedSubs;
        lock (subs)
        {
            typedSubs = subs.Cast<ISubscription<TEvent>>().ToList();
        }

        foreach (var sub in typedSubs)
        {
            if (!sub.IsAlive || !sub.Invoke(evt))
            {
                // cleanup dead weak refs
                lock (subs)
                {
                    subs.Remove(sub);
                }
            }
        }
    }
    
    
  
}
