using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.EventHub;

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