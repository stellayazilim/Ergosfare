using System;

namespace Ergosfare.Core.Abstractions.SignalHub;

// Proxy to support += syntax
public sealed class ProxySignal<TEvent>(ISignalHub hub) where TEvent : Signal
{
    private readonly ISignalHub _hub = hub;
    public static ProxySignal<TEvent> operator +(ProxySignal<TEvent> proxy, Action<TEvent> handler)
    {
        proxy._hub.Subscribe(handler);
        return proxy;
    }

    public static ProxySignal<TEvent> operator -(ProxySignal<TEvent> proxy, Action<TEvent> handler)
    {
        proxy._hub.Unsubscribe(handler);
        return proxy;
    }
}