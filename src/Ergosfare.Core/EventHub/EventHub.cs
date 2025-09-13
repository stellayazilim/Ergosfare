using System.Collections.Concurrent;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Events;

namespace Ergosfare.Core.EventHub;
public class EventHub: IEventHub, IHasProxyEvents
{
    private readonly object _lock = new();
    
    private readonly ConcurrentDictionary<Type, List<object>> _subscriptions = new();

    public EventHub()
    {
        BeginPipelineEvent = new ProxyEvent<BeginPipelineEvent>(this);
        BeginPreInterceptingEvent = new ProxyEvent<BeginPreInterceptingEvent>(this);
        BeginPreInterceptorInvocationEvent = new ProxyEvent<BeginPreInterceptorInvocationEvent>(this);
        FinishPreInterceptorInvocationEvent = new ProxyEvent<FinishPreInterceptorInvocationEvent>(this);
        FinishPreInterceptingWithExceptionEvent = new ProxyEvent<FinishPreInterceptingWithExceptionEvent>(this);
        FinishPreInterceptingEvent = new ProxyEvent<FinishPreInterceptingEvent>(this);
        
        BeginHandlingEvent = new ProxyEvent<BeginHandlingEvent>(this);
        BeginHandlerInvocationEvent = new ProxyEvent<BeginHandlerInvocationEvent>(this);
        FinishHandlerInvocationEvent = new ProxyEvent<FinishHandlerInvocationEvent>(this);
        FinishHandlingWithExceptionEvent = new ProxyEvent<FinishHandlingWithExceptionEvent>(this);
        FinishHandlingEvent = new ProxyEvent<FinishHandlingEvent>(this);
        
        BeginPostInterceptingEvent = new ProxyEvent<BeginPostInterceptingEvent>(this);
        BeginPostInterceptorInvocationEvent = new ProxyEvent<BeginPostInterceptorInvocationEvent>(this);
        FinishPostInterceptorInvocationEvent = new ProxyEvent<FinishPostInterceptorInvocationEvent>(this);
        FinishPostInterceptingWithExceptionEvent = new ProxyEvent<FinishPostInterceptingWithExceptionEvent>(this);
        FinishPostInterceptingEvent = new ProxyEvent<FinishPostInterceptingEvent>(this);
        
        BeginExceptionInterceptingEvent = new ProxyEvent<BeginExceptionInterceptingEvent>(this);
        BeginExceptionInterceptorInvocationEvent = new ProxyEvent<BeginExceptionInterceptorInvocationEvent>(this);
        FinishExceptionInterceptorInvocationEvent = new ProxyEvent<FinishExceptionInterceptorInvocationEvent>(this);
        FinishExceptionInterceptingEvent = new ProxyEvent<FinishExceptionInterceptingEvent>(this);
        
        FinishPipelineEvent = new ProxyEvent<FinishPipelineEvent>(this);
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


 
    
    // pipeline start event  
    public ProxyEvent<BeginPipelineEvent> BeginPipelineEvent { get; }
    
    // pre intercepting
    public ProxyEvent<BeginPreInterceptingEvent> BeginPreInterceptingEvent { get; }
    public ProxyEvent<BeginPreInterceptorInvocationEvent> BeginPreInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPreInterceptorInvocationEvent> FinishPreInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPreInterceptingWithExceptionEvent> FinishPreInterceptingWithExceptionEvent { get; }
    public ProxyEvent<FinishPreInterceptingEvent>  FinishPreInterceptingEvent { get; }
    // handler events
    public ProxyEvent<BeginHandlingEvent> BeginHandlingEvent { get; }
    public ProxyEvent<BeginHandlerInvocationEvent> BeginHandlerInvocationEvent { get; }
    public ProxyEvent<FinishHandlerInvocationEvent> FinishHandlerInvocationEvent { get; }
    public ProxyEvent<FinishHandlingWithExceptionEvent> FinishHandlingWithExceptionEvent { get; }
    public ProxyEvent<FinishHandlingEvent>  FinishHandlingEvent { get; }
    // post intercepting
    public ProxyEvent<BeginPostInterceptingEvent> BeginPostInterceptingEvent { get; }
    public ProxyEvent<BeginPostInterceptorInvocationEvent> BeginPostInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPostInterceptorInvocationEvent> FinishPostInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPostInterceptingWithExceptionEvent> FinishPostInterceptingWithExceptionEvent { get; }
    public ProxyEvent<FinishPostInterceptingEvent> FinishPostInterceptingEvent { get; }
    // exception intercepting 
    public ProxyEvent<BeginExceptionInterceptingEvent> BeginExceptionInterceptingEvent { get; }
    public ProxyEvent<BeginExceptionInterceptorInvocationEvent> BeginExceptionInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishExceptionInterceptorInvocationEvent> FinishExceptionInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishExceptionInterceptingEvent> FinishExceptionInterceptingEvent { get; }
    // finish pipeline
    public ProxyEvent<FinishPipelineEvent> FinishPipelineEvent { get; }
}

