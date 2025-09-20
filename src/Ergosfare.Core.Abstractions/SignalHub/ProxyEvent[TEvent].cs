using System;

namespace Ergosfare.Core.Abstractions.SignalHub;

/// <summary>
/// A proxy wrapper around <see cref="ISignalHub"/> that enables += and -= syntax for subscribing/unsubscribing to signals.
/// </summary>
/// <typeparam name="TEvent">The type of signal to subscribe to.</typeparam>
public sealed class ProxySignal<TEvent>(ISignalHub hub) where TEvent : Signal
{
    private readonly ISignalHub _hub = hub;
    
    
    /// <summary>
    /// Subscribes a handler to the signal using the += syntax.
    /// </summary>
    /// <param name="proxy">The proxy signal instance.</param>
    /// <param name="handler">The handler to subscribe.</param>
    /// <returns>The same proxy instance.</returns>
    public static ProxySignal<TEvent> operator +(ProxySignal<TEvent> proxy, Action<TEvent> handler)
    {
        proxy._hub.Subscribe(handler);
        return proxy;
    }

    
    
    /// <summary>
    /// Unsubscribes a handler from the signal using the -= syntax.
    /// </summary>
    /// <param name="proxy">The proxy signal instance.</param>
    /// <param name="handler">The handler to unsubscribe.</param>
    /// <returns>The same proxy instance.</returns>
    public static ProxySignal<TEvent> operator -(ProxySignal<TEvent> proxy, Action<TEvent> handler)
    {
        proxy._hub.Unsubscribe(handler);
        return proxy;
    }
}