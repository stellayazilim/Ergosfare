using System;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub;


/// <summary>
/// Represents a strong subscription to a <typeparamref name="TEvent"/> in the event hub.
/// Holds a strong reference to the provided <see cref="Action{TEvent}"/>, 
/// ensuring the handler is always alive until explicitly disposed.
/// </summary>
/// <typeparam name="TEvent">The type of the event, must inherit from <see cref="Signal"/>.</typeparam>
internal sealed class StrongSubscription<TEvent> : ISubscription<TEvent> where TEvent : Signal
{
    
    /// <summary>
    /// The strongly referenced action to invoke when the event occurs.
    /// </summary>
    private readonly Action<TEvent> _action;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="StrongSubscription{TEvent}"/> class
    /// with the specified action to invoke when the event occurs.
    /// </summary>
    /// <param name="action">The action to execute when the event is invoked.</param>
    public StrongSubscription(Action<TEvent> action) => _action = action;
    
    /// <summary>
    /// Invokes the subscription's action with the provided event.
    /// </summary>
    /// <param name="signal">The event instance to pass to the action.</param>
    /// <returns>Always returns <c>true</c>, indicating the action was invoked successfully.</returns>
    public bool Invoke(TEvent signal)
    {
        _action(signal);
        return true;
    }
    
    /// <summary>
    /// Gets a value indicating whether the subscription is still alive.
    /// For strong subscriptions, this always returns <c>true</c>.
    /// </summary>
    public bool IsAlive => true;
  
    /// <summary>
    /// Determines whether the provided action matches the action stored in this subscription.
    /// </summary>
    /// <param name="action">The action to compare against the stored action.</param>
    /// <returns><c>true</c> if the actions are equal; otherwise, <c>false</c>.</returns>
    public bool Matches(Action<TEvent> action)
    {
        return _action.Equals(action);
    }

    /// <summary>
    /// Disposes the subscription. For <see cref="StrongSubscription{TEvent}"/>, this is a no-op.
    /// </summary>
    public void Dispose() { /* nothing to dispose */ }
}
