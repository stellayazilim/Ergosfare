using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub;
public class SignalHub: ISignalHub, IHasProxySignals
{
    private readonly object _lock = new();
    
    private readonly ConcurrentDictionary<Type, List<object>> _subscriptions = new();

    public SignalHub()
    {
        BeginPipelineSignal = new ProxySignal<BeginPipelineSignal>(this);
        BeginPreInterceptingSignal = new ProxySignal<BeginPreInterceptingSignal>(this);
        BeginPreInterceptorInvocationSignal = new ProxySignal<BeginPreInterceptorInvocationSignal>(this);
        FinishPreInterceptorInvocationSignal = new ProxySignal<FinishPreInterceptorInvocationSignal>(this);
        FinishPreInterceptingWithExceptionSignal = new ProxySignal<FinishPreInterceptingWithExceptionSignal>(this);
        FinishPreInterceptingSignal = new ProxySignal<FinishPreInterceptingSignal>(this);
        
        BeginHandlingSignal = new ProxySignal<BeginHandlingSignal>(this);
        BeginHandlerInvocationSignal = new ProxySignal<BeginHandlerInvocationSignal>(this);
        FinishHandlerInvocationSignal = new ProxySignal<FinishHandlerInvocationSignal>(this);
        FinishHandlingWithExceptionSignal = new ProxySignal<FinishHandlingWithExceptionSignal>(this);
        FinishHandlingSignal = new ProxySignal<FinishHandlingSignal>(this);
        
        BeginPostInterceptingSignal = new ProxySignal<BeginPostInterceptingSignal>(this);
        BeginPostInterceptorInvocationSignal = new ProxySignal<BeginPostInterceptorInvocationSignal>(this);
        FinishPostInterceptorInvocationSignal = new ProxySignal<FinishPostInterceptorInvocationSignal>(this);
        FinishPostInterceptingWithExceptionSignal = new ProxySignal<FinishPostInterceptingWithExceptionSignal>(this);
        FinishPostInterceptingSignal = new ProxySignal<FinishPostInterceptingSignal>(this);
        
        BeginExceptionInterceptingSignal = new ProxySignal<BeginExceptionInterceptingSignal>(this);
        BeginExceptionInterceptorInvocationSignal = new ProxySignal<BeginExceptionInterceptorInvocationSignal>(this);
        FinishExceptionInterceptorInvocationSignal = new ProxySignal<FinishExceptionInterceptorInvocationSignal>(this);
        FinishExceptionInterceptingSignal = new ProxySignal<FinishExceptionInterceptingSignal>(this);
        
        FinishPipelineSignal = new ProxySignal<FinishPipelineSignal>(this);
    }
    public IDisposable Subscribe<TSignal>(Action<TSignal> handler, bool useWeakReference = false)
        where TSignal : Signal
    {
        ISubscription<TSignal> subscription = useWeakReference
            ? new WeakSubscription<TSignal>(handler)
            : new StrongSubscription<TSignal>(handler);

        var subs = _subscriptions.GetOrAdd(typeof(TSignal), _ => new List<object>());
        lock (subs)
        {
            subs.Add(subscription);
        }
        return subscription;
    }

    public void Publish<TSignal>(TSignal evt) where TSignal : Signal
    {
        if (!_subscriptions.TryGetValue(typeof(TSignal), out var subs))
            return;

        List<ISubscription<TSignal>> typedSubs;
        lock (subs)
        {
            typedSubs = subs.Cast<ISubscription<TSignal>>().ToList();
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
        
        
    public void Unsubscribe<TSignal>(Action<TSignal> handler) where TSignal : Signal
    {
        if (_subscriptions.TryGetValue(typeof(TSignal), out var subs))
        {
            lock (subs)
            {
                subs.RemoveAll(
                    s =>  {
                    if (s is ISubscription<TSignal> typed)
                    {
                        return typed.Matches(handler);
                    }
                    return false;
                });
            }
        }
    }


 
    
    // pipeline start signal  
    public ProxySignal<BeginPipelineSignal> BeginPipelineSignal { get; }
    
    // pre intercepting
    public ProxySignal<BeginPreInterceptingSignal> BeginPreInterceptingSignal { get; }
    public ProxySignal<BeginPreInterceptorInvocationSignal> BeginPreInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPreInterceptorInvocationSignal> FinishPreInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPreInterceptingWithExceptionSignal> FinishPreInterceptingWithExceptionSignal { get; }
    public ProxySignal<FinishPreInterceptingSignal>  FinishPreInterceptingSignal { get; }
    // handler signals
    public ProxySignal<BeginHandlingSignal> BeginHandlingSignal { get; }
    public ProxySignal<BeginHandlerInvocationSignal> BeginHandlerInvocationSignal { get; }
    public ProxySignal<FinishHandlerInvocationSignal> FinishHandlerInvocationSignal { get; }
    public ProxySignal<FinishHandlingWithExceptionSignal> FinishHandlingWithExceptionSignal { get; }
    public ProxySignal<FinishHandlingSignal>  FinishHandlingSignal { get; }
    // post intercepting
    public ProxySignal<BeginPostInterceptingSignal> BeginPostInterceptingSignal { get; }
    public ProxySignal<BeginPostInterceptorInvocationSignal> BeginPostInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPostInterceptorInvocationSignal> FinishPostInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPostInterceptingWithExceptionSignal> FinishPostInterceptingWithExceptionSignal { get; }
    public ProxySignal<FinishPostInterceptingSignal> FinishPostInterceptingSignal { get; }
    // exception intercepting 
    public ProxySignal<BeginExceptionInterceptingSignal> BeginExceptionInterceptingSignal { get; }
    public ProxySignal<BeginExceptionInterceptorInvocationSignal> BeginExceptionInterceptorInvocationSignal { get; }
    public ProxySignal<FinishExceptionInterceptorInvocationSignal> FinishExceptionInterceptorInvocationSignal { get; }
    public ProxySignal<FinishExceptionInterceptingSignal> FinishExceptionInterceptingSignal { get; }
    // finish pipeline
    public ProxySignal<FinishPipelineSignal> FinishPipelineSignal { get; }
}

