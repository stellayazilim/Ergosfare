namespace Stella.Ergosfare.Core.Internal.Caching;

/// <summary>
/// A cache key made of a message type and the group names requested with it, compared
/// ordinally and in order.
/// </summary>
/// <param name="messageType">The message type the key is for.</param>
/// <param name="groups">The requested group names, in order.</param>
/// <remarks>
/// A struct, so looking a pipeline up by (message, groups) allocates nothing.
/// </remarks>
internal readonly struct GroupedDependenciesKey(Type messageType, string[] groups) : IEquatable<GroupedDependenciesKey>
{
    private readonly Type _messageType = messageType;
    private readonly string[] _groups = groups;

    /// <summary>
    /// The message type the key is for.
    /// </summary>
    public Type MessageType => _messageType;

    /// <summary>
    /// The requested group names, in order.
    /// </summary>
    public string[] Groups => _groups;

    /// <summary>
    /// Reports whether <paramref name="other"/> names the same message type and the same
    /// groups in the same order.
    /// </summary>
    /// <param name="other">The key to compare against.</param>
    /// <returns><c>true</c> when both keys select the same pipeline.</returns>
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

    /// <summary>
    /// Reports whether <paramref name="obj"/> is an equal key.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is a key selecting the same pipeline.</returns>
    public override bool Equals(object? obj) => obj is GroupedDependenciesKey other && Equals(other);

    /// <summary>
    /// Returns a hash code over the message type and the group names, in order.
    /// </summary>
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
