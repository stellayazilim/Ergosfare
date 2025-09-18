using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub;


/// <summary>
/// Represents the base class for all events in the event hub.
/// Supports equality comparison based on event components, maintains a timestamp,
/// and allows tracking related events.
/// </summary>
public abstract class Signal: IEquatable<Signal>
{
    
    /// <summary>
    /// Stores related events that are linked to this event.
    /// </summary>
    private readonly List<Signal> _relatedEvents = new();
    
    /// <summary>
    /// Gets a read-only list of events related to this event.
    /// </summary>
    public IReadOnlyList<Signal> RelatedEvents =>  _relatedEvents.AsReadOnly(); 
    
    /// <summary>
    /// Gets the UTC timestamp of when this event was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// Provides the components of this event that are used for equality comparison.
    /// Derived classes must override this method to specify which properties define equality.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{Object}"/> of objects representing equality components.</returns>
    public abstract IEnumerable<object> GetEqualityComponents();
    
    /// <summary>
    /// Adds a related <see cref="HubSignal"/> to this event's related events list.
    /// </summary>
    /// <param name="hubSignal">The event to add as related.</param>
    public void Add(Signal hubSignal)
    {
        _relatedEvents.Add(hubSignal);
    }

    /// <summary>
    /// Adds multiple related <see cref="HubSignal"/> instances to this event's related events list.
    /// </summary>
    /// <param name="hubSignals">The events to add as related.</param>
    public void AddRange(params IEnumerable<Signal> hubSignals)
    {
        _relatedEvents.AddRange(hubSignals);
    }
    
    
    /// <summary>
    /// Determines whether the specified object is equal to the current event.
    /// Equality is determined by comparing all components returned by <see cref="GetEqualityComponents"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the current event.</param>
    /// <returns><c>true</c> if the specified object is equal to the current event; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var valueObject = (Signal)obj;
        return GetEqualityComponents()
            .SequenceEqual(valueObject.GetEqualityComponents());
    }

    
    /// <summary>
    /// Determines whether two <see cref="Signal"/> instances are equal.
    /// </summary>
    /// <param name="left">The first event to compare.</param>
    /// <param name="right">The second event to compare.</param>
    /// <returns><c>true</c> if the events are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Signal left, Signal right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Determines whether two <see cref="HubSignal"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first event to compare.</param>
    /// <param name="right">The second event to compare.</param>
    /// <returns><c>true</c> if the events are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Signal left, Signal right)
    {
        return !Equals(left, right);
    }

    /// <summary>
    /// Returns a hash code based on the equality components of this event.
    /// </summary>
    /// <returns>An integer hash code.</returns>
    public override int GetHashCode()
    {
        return  GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    /// <summary>
    /// Determines whether the specified <see cref="HubSignal"/> is equal to the current event.
    /// </summary>
    /// <param name="other">The event to compare with the current event.</param>
    /// <returns><c>true</c> if the specified event is equal to the current event; otherwise, <c>false</c>.</returns>
    public bool Equals(Signal? other)
    {
        return Equals((object?)other);
    }
    
}