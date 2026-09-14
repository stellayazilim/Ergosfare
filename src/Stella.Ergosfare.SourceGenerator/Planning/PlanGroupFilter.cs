using System.Collections.Immutable;
using System.Text;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
/// Decides which participants one plan carries, and under what test.
/// </summary>
/// <param name="target">The group set this plan is compiled for.</param>
/// <param name="filtering">
/// Whether this is the plan that serves dispatches whose groups are only known at runtime.
/// </param>
/// <remarks>
/// A plan compiled for a known set decides here and simply leaves the rest out. The
/// filtering plan takes every participant instead and gives each call the name of a boolean
/// its body computes once.
/// </remarks>
internal sealed class PlanGroupFilter(ImmutableArray<string> target, bool filtering)
{
    private readonly Dictionary<string, string> _guardsBySignature = new(StringComparer.Ordinal);
    private readonly List<StagedGroupGuardModel> _guards = [];
    private readonly HashSet<string> _covered = new(StringComparer.Ordinal);

    /// <summary>
    /// The group set this plan is compiled for.
    /// </summary>
    public ImmutableArray<string> Target { get; } = target;

    /// <summary>
    /// Whether this plan decides participation at runtime rather than now.
    /// </summary>
    public bool Filtering { get; } = filtering;

    /// <summary>
    /// The tests the plan's body computes once at the top, one per distinct group
    /// declaration among its participants.
    /// </summary>
    public ImmutableArray<StagedGroupGuardModel> Guards => ImmutableArray.CreateRange(_guards);

    /// <summary>
    /// Every group the filtering plan can be asked about — all the groups its participants
    /// declare between them.
    /// </summary>
    /// <remarks>
    /// The runtime checks the plan against the composition over exactly these, which is the
    /// one set that reproduces the participants its body holds.
    /// </remarks>
    public ImmutableArray<string> CoveredGroups
    {
        get
        {
            var names = new List<string>(_covered);

            return GroupNames.Normalize(names);

        }
    }

    /// <summary>
    /// Decides whether the plan carries a participant, and under which test.
    /// </summary>
    /// <param name="participant">The participant to consider.</param>
    /// <param name="guard">
    /// The local holding this participant's group test, when this returns <c>true</c> on a
    /// filtering plan; <c>null</c> when the call needs no test.
    /// </param>
    /// <returns><c>true</c> when the plan carries the participant.</returns>
    /// <remarks>
    /// A plan compiled for a known set answers <c>false</c> for anyone that set does not
    /// select. The filtering plan takes everyone and names their test, reusing one local for
    /// participants that declare the same groups.
    /// </remarks>
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
    /// Builds the call that computes one participant's group test.
    /// </summary>
    /// <param name="participant">The participant the test is for.</param>
    /// <returns>The call to write.</returns>
    /// <remarks>
    /// A participant declaring no groups gets the default-group test, one declaring a single
    /// group gets the overload taking one name, and only several groups need an array.
    /// </remarks>
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

    /// <summary>
    /// Writes a group name as a C# string literal.
    /// </summary>
    /// <param name="value">The group name.</param>
    /// <returns>The literal, quoted and escaped.</returns>
    private static string Quote(string value)
        => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>
    /// Reports whether a participant runs under a group set.
    /// </summary>
    /// <param name="participant">The participant to test.</param>
    /// <param name="targetGroups">The groups being dispatched under.</param>
    /// <returns><c>true</c> when any declared group is any requested group.</returns>
    /// <remarks>
    /// The same test the runtime makes: a participant declaring no groups belongs to the
    /// default group, and requesting no groups means requesting the default one.
    /// </remarks>
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
