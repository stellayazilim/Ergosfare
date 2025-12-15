using System;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub;

/// <summary>
/// Defines a hub for publishing and subscribing to signals.
/// </summary>
public interface ISignalHub
{
    /// <summary>
    /// Subscribes to a signal of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of signal to subscribe to.</typeparam>
    /// <param name="handler">The action to invoke when the signal is published.</param>
    /// <param name="useWeakReference">If true, the subscription uses a weak reference to avoid preventing garbage collection.</param>
    /// <returns>An <see cref="IDisposable"/> that can be used to unsubscribe.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool useWeakReference = false)
        where TEvent : Signal;

    
    /// <summary>
    /// Publishes a signal of type <typeparamref name="TEvent"/> to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of signal to publish.</typeparam>
    /// <param name="evt">The signal instance to publish.</param>
    void Publish<TEvent>(TEvent evt) where TEvent : Signal;

    
    /// <summary>
    /// Unsubscribes a previously registered handler for signals of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of signal for which to remove the subscription.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : Signal;
    
}
