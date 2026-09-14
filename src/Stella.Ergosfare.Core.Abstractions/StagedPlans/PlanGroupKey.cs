namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>A set key over existing group names; lookup never sorts or copies them.</summary>
internal readonly struct PlanGroupKey(IReadOnlyList<string>? groups, bool filtering = false)
    : IEquatable<PlanGroupKey>
{
    private readonly IReadOnlyList<string>? _groups = groups;
    private readonly bool _filtering = filtering;
    public static PlanGroupKey Default => default;
    public static PlanGroupKey Filtering => new(null, true);
    internal IReadOnlyList<string> Groups => _groups ?? Array.Empty<string>();

    public bool Equals(PlanGroupKey other)
        => _filtering == other._filtering && ContainsAll(_groups, other._groups)
            && ContainsAll(other._groups, _groups);

    public override bool Equals(object? obj) => obj is PlanGroupKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = _filtering ? int.MinValue : 0;
        if (_groups is null) return hash;
        for (var i = 0; i < _groups.Count; i++)
        {
            var duplicate = false;
            for (var j = 0; j < i; j++)
                if (string.Equals(_groups[i], _groups[j], StringComparison.Ordinal))
                { duplicate = true; break; }
            if (!duplicate) hash = unchecked(hash + StringComparer.Ordinal.GetHashCode(_groups[i]));
        }
        return hash;
    }

    private static bool ContainsAll(IReadOnlyList<string>? source, IReadOnlyList<string>? target)
    {
        if (source is null) return true;
        for (var i = 0; i < source.Count; i++)
        {
            var found = false;
            if (target is not null)
                for (var j = 0; j < target.Count; j++)
                    if (string.Equals(source[i], target[j], StringComparison.Ordinal))
                    { found = true; break; }
            if (!found) return false;
        }
        return true;
    }
}
