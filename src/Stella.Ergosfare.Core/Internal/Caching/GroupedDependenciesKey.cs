namespace Stella.Ergosfare.Core.Internal.Caching;

/// <summary>
/// Allocation-free composite key: message type plus ordered group names,
/// compared with ordinal sequence equality.
/// </summary>
internal readonly struct GroupedDependenciesKey(Type messageType, string[] groups) : IEquatable<GroupedDependenciesKey>
{
    private readonly Type _messageType = messageType;
    private readonly string[] _groups = groups;

    public bool Equals(GroupedDependenciesKey other)
    {
        if (_messageType != other._messageType || _groups.Length != other._groups.Length)
        {
            return false;
        }

        for (var i = 0; i < _groups.Length; i++)
        {
            if (!string.Equals(_groups[i], other._groups[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is GroupedDependenciesKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_messageType);

        foreach (var group in _groups)
        {
            hash.Add(group, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
