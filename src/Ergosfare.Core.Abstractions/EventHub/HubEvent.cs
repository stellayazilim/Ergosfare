using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.EventHub;

public abstract class HubEvent: IEquatable<HubEvent>
{
    private readonly List<HubEvent> _relatedEvents = new();
    public IReadOnlyList<HubEvent> RelatedEvents =>  _relatedEvents.AsReadOnly(); 
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public abstract IEnumerable<object> GetEqualityComponents();
    
    public void Add(HubEvent hubEvent)
    {
        _relatedEvents.Add(hubEvent);
    }

    public void AddRange(IEnumerable<HubEvent> hubEvents)
    {
        _relatedEvents.AddRange(hubEvents);
    }
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var valueObject = (HubEvent)obj;
        return GetEqualityComponents()
            .SequenceEqual(valueObject.GetEqualityComponents());
    }

    public static bool operator ==(HubEvent left, HubEvent right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HubEvent left, HubEvent right)
    {
        return !Equals(left, right);
    }

    public override int GetHashCode()
    {
        var components = GetEqualityComponents().ToList(); 
        components.Add(Timestamp);
        return  components
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public bool Equals(HubEvent? other)
    {
        return Equals((object?)other);
    }
    
}