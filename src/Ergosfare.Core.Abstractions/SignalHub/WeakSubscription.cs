using System;

namespace Ergosfare.Core.Abstractions.SignalHub;


/// <summary>
/// Represents a weak subscription to events of type <typeparamref name="TEvent"/>.
/// Holds a <see cref="WeakReference"/> to the event handler to allow garbage collection
/// when there are no other references to the handler.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to, constrained to <see cref="Signal"/>.</typeparam>
internal sealed class WeakSubscription<TEvent> : ISubscription<TEvent> where TEvent : Signal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeakSubscription{TEvent}"/> class
    /// that wraps the specified event handler with a weak reference.
    /// </summary>
    /// <param name="action">The event handler to subscribe to.</param>
    private readonly WeakReference<Action<TEvent>> _weak;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="WeakSubscription{TEvent}"/> class
    /// that wraps the specified event handler with a weak reference.
    /// </summary>
    /// <param name="action">The event handler to subscribe to.</param>
    public WeakSubscription(Action<TEvent> action) => _weak = new(action);
    
    /// <summary>
    /// Invokes the subscribed event handler if it is still alive.
    /// </summary>
    /// <param name="evt">The event to pass to the handler.</param>
    /// <returns>
    /// <c>true</c> if the handler was successfully invoked; <c>false</c> if the handler
    /// has been garbage collected and is no longer alive.
    /// </returns>
    public bool Invoke(TEvent evt)
    {
        if (_weak.TryGetTarget(out var target))
        {
            target(evt);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the stored handler matches the specified action.
    /// </summary>
    /// <param name="action">The action to compare with the stored handler.</param>
    /// <returns>
    /// <c>true</c> if the stored handler is alive and equals the specified action; otherwise, <c>false</c>.
    /// </returns>
    public bool Matches(Action<TEvent> action)
    {
        return _weak.TryGetTarget(out var target) && target.Equals(action);
    }

    /// <summary>
    /// Gets a value indicating whether the subscribed handler is still alive.
    /// </summary>
    public bool IsAlive => _weak.TryGetTarget(out _);
    
 
    /// <summary>
    /// Disposes the subscription. No resources need to be released explicitly for this implementation.
    /// </summary>
    public void Dispose() { /* nothing to dispose */ }
}