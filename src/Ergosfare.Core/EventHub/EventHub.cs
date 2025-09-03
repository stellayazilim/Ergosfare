using System.Collections.Concurrent;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.EventHub;
public class EventHub: IEventHub
{
        private readonly object _lock = new();
        
        private readonly ConcurrentDictionary<Type, List<object>> _subscriptions = new();
    
        public EventHub()
        {
            PreInterceptorBeingInvokeEventProxy = new ProxyEvent<PreInterceptorBeingInvokeEvent>(this);
        }
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool useWeakReference = false)
            where TEvent : HubEvent
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

        public void Publish<TEvent>(TEvent evt) where TEvent : HubEvent
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
                    lock (subs)
                    {
                        subs.Remove(sub);
                    }
                }
            }
        }
        
        
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : HubEvent
        {
            if (_subscriptions.TryGetValue(typeof(TEvent), out var subs))
            {
                lock (subs)
                {
                    subs.RemoveAll(
                        s =>  {
                        if (s is ISubscription<TEvent> typed)
                        {
                            return typed.Matches(handler);
                        }
                        return false;
                    });
                }
            }
        }


        public ProxyEvent<PreInterceptorBeingInvokeEvent> PreInterceptorBeingInvokeEventProxy { get; }
        
    // Proxy to support += syntax
    public sealed class ProxyEvent<TEvent>(IEventHub hub) where TEvent : HubEvent
    {
        private readonly IEventHub _hub = hub;
        public static ProxyEvent<TEvent> operator +(ProxyEvent<TEvent> proxy, Action<TEvent> handler)
        {
            proxy._hub.Subscribe(handler);
            return proxy;
        }

        public static ProxyEvent<TEvent> operator -(ProxyEvent<TEvent> proxy, Action<TEvent> handler)
        {
            proxy._hub.Unsubscribe(handler);
            return proxy;
        }
    }

    public sealed class PreInterceptorBeingInvokeEvent : HubEvent
    {
        public required string InterceptorName { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return Timestamp;
            yield return InterceptorName;
        }
    }

    
}

