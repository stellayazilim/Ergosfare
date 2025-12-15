using System;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub;

/// <summary>
/// Represents a subscription to a <typeparamref name="TSignal"/> in the event hub.
/// Provides methods to invoke the subscription, check its state, and match against a specific action.
/// </summary>
/// <typeparam name="TSignal">The type of event this subscription listens to. Must derive from <see cref="Signal"/>.</typeparam>
public interface ISubscription<TSignal> : IDisposable where TSignal : Signal
{
    /// <summary>
    /// Invokes the subscription with the specified event.
    /// </summary>
    /// <param name="event">The event instance to deliver to the subscriber.</param>
    /// <returns>
    /// <c>true</c> if the subscription successfully handled the event; otherwise, <c>false</c> (e.g., if the subscription is no longer alive).
    /// </returns>
    bool Invoke(TSignal @event);
    
    /// <summary>
    /// Gets a value indicating whether the subscription is still alive.
    /// A subscription may become not alive if it uses a weak reference that has been garbage collected.
    /// </summary>
    bool IsAlive { get; }

    
    /// <summary>
    /// Determines whether the subscription matches the specified action.
    /// Useful for removing or comparing subscriptions.
    /// </summary>
    /// <param name="action">The action to compare against the subscription's target.</param>
    /// <returns><c>true</c> if the subscription corresponds to the given action; otherwise, <c>false</c>.</returns>
    bool Matches(Action<TSignal> action);
}