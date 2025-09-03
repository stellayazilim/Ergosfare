using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.EventHub;

public abstract class HubEvent: IEquatable<HubEvent>
{
    public abstract IEnumerable<object> GetEqualityComponents();
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
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public bool Equals(HubEvent? other)
    {
        return Equals((object?)other);
    }

}