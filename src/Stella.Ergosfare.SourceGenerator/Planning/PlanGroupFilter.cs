using System.Collections.Immutable;
using System.Text;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
///     How one plan decides which participants it carries. A plan keyed by a proven group
///     set decides at compile time and simply omits the rest; the filtering plan — the one
///     serving dispatches whose set is a runtime value — carries everyone and hands each
///     call the name of a boolean the body evaluates once.
/// </summary>
internal sealed class PlanGroupFilter(ImmutableArray<string> target, bool filtering)
{
    private readonly Dictionary<string, string> _guardsBySignature = new(StringComparer.Ordinal);
    private readonly List<StagedGroupGuardModel> _guards = [];
    private readonly HashSet<string> _covered = new(StringComparer.Ordinal);

    public ImmutableArray<string> Target { get; } = target;

    public bool Filtering { get; } = filtering;

    public ImmutableArray<StagedGroupGuardModel> Guards => ImmutableArray.CreateRange(_guards);

    /// <summary>
    ///     Every group the filtering plan can be asked about — the union of what its
    ///     participants declare. The runtime gate validates the plan against the
    ///     composition over exactly these, which is the only set that reproduces the
    ///     participants the body carries.
    /// </summary>
    public ImmutableArray<string> CoveredGroups
    {
        get
        {
            var names = new List<string>(_covered);

            return GroupNames.Normalize(names);

        }
    }

    /// <summary>
    ///     Whether the plan carries this participant, and under which guard. A keyed plan
    ///     answers false for anyone its set does not select; the filtering plan takes
    ///     everyone and names their test.
    /// </summary>
    public bool TryInclude(RegistrableTypeModel participant, out string? guard)
    {
        if (!Filtering)
        {
            guard = null;
            return Participates(participant, Target);
        }

        var signature = participant.GroupNames.IsEmpty
            ? GroupNames.Default
            : string.Join("\u001f", participant.GroupNames);

        if (participant.GroupNames.IsEmpty)
        {
            _covered.Add(GroupNames.Default);
        }
        else
        {
            foreach (var name in participant.GroupNames)
            {
                _covered.Add(name);
            }
        }

        if (!_guardsBySignature.TryGetValue(signature, out guard))
        {
            guard = "group" + _guards.Count;
            _guardsBySignature.Add(signature, guard);
            _guards.Add(new StagedGroupGuardModel(guard, GuardExpression(participant)));
        }

        return true;
    }

    /// <summary>
    ///     The call filling one guard: the single-group overload for the common shape, the
    ///     array one when a participant declares several, and the default-group test for a
    ///     participant that declares none.
    /// </summary>
    private static string GuardExpression(RegistrableTypeModel participant)
    {
        const string helper = "global::Stella.Ergosfare.Core.Abstractions.StagedPlans.PlanGroups.";

        if (participant.GroupNames.IsEmpty)
        {
            return helper + "MatchesDefault(groups)";
        }

        if (participant.GroupNames.Length == 1)
        {
            return helper + "Matches(groups, " + Quote(participant.GroupNames[0]) + ")";
        }

        var sb = new StringBuilder(helper).Append("Matches(groups, new string[] { ");

        for (var i = 0; i < participant.GroupNames.Length; i++)
        {
            sb.Append(i == 0 ? string.Empty : ", ").Append(Quote(participant.GroupNames[i]));
        }

        return sb.Append(" })").ToString();
    }

    private static string Quote(string value)
        => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
    /// <summary>
    ///     Whether a participant runs under a target group set, mirroring the runtime's
    ///     any-of × any-of test: a participant declaring no <c>[Group]</c> belongs to the
    ///     default group, and an empty target set IS the default group.
    /// </summary>
    internal static bool Participates(RegistrableTypeModel participant, ImmutableArray<string> targetGroups)
    {
        if (participant.GroupNames.IsEmpty)
        {
            if (targetGroups.IsEmpty)
            {
                return true;
            }

            foreach (var target in targetGroups)
            {
                if (string.Equals(target, GroupNames.Default, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        if (targetGroups.IsEmpty)
        {
            foreach (var declared in participant.GroupNames)
            {
                if (string.Equals(declared, GroupNames.Default, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var declared in participant.GroupNames)
        {
            foreach (var target in targetGroups)
            {
                if (string.Equals(declared, target, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
