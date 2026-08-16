using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The group test a group-filtering plan bakes in front of each participant. Emitted code
/// calls these three methods and nothing else decides whether a participant runs, so what
/// they answer is the filtering semantics themselves — not an implementation detail of one.
/// </summary>
/// <remarks>
/// The asymmetry worth remembering: an <b>empty request</b> means the default group, so a
/// participant declaring no group runs and one declaring any group does not. It does not mean
/// "no filtering" here — that decision was made before the plan was reached, by the dispatch
/// surface turning an empty <c>GroupSet</c> into a null filter.
/// </remarks>
public class PlanGroupsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void UngroupedParticipant_RunsUnderTheDefaultGroupAndNothingElse()
    {
        // An empty request IS the default group, which is where an ungrouped participant lives.
        Assert.True(PlanGroups.MatchesDefault([]));
        Assert.True(PlanGroups.MatchesDefault([GroupAttribute.DefaultGroupName]));

        // Named alongside others is still named: the default group only has to be in the set.
        Assert.True(PlanGroups.MatchesDefault(["reporting", GroupAttribute.DefaultGroupName]));

        Assert.False(PlanGroups.MatchesDefault(["reporting"]));
        Assert.False(PlanGroups.MatchesDefault(["reporting", "auditing"]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void SingleGroupParticipant_RunsOnlyWhenItsGroupWasAskedFor()
    {
        Assert.True(PlanGroups.Matches(["reporting"], "reporting"));
        Assert.True(PlanGroups.Matches(["auditing", "reporting"], "reporting"));

        Assert.False(PlanGroups.Matches(["auditing"], "reporting"));

        // Ordinal, so case is not a near miss — it is a different group.
        Assert.False(PlanGroups.Matches(["Reporting"], "reporting"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void SingleGroupParticipant_UnderAnEmptyRequest_RunsOnlyIfItNamedTheDefaultGroup()
    {
        // The empty request is the default group's pipeline: a participant that named a
        // group is not in it, and one that spelled the default group's name out is.
        Assert.False(PlanGroups.Matches([], "reporting"));
        Assert.True(PlanGroups.Matches([], GroupAttribute.DefaultGroupName));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MultiGroupParticipant_RunsWhenAnyOfItsGroupsWasAskedFor()
    {
        Assert.True(PlanGroups.Matches(["reporting"], ["auditing", "reporting"]));
        Assert.True(PlanGroups.Matches(["auditing", "billing"], ["billing"]));

        Assert.False(PlanGroups.Matches(["reporting"], ["auditing", "billing"]));

        // Declaring nothing is not declaring the default group — an empty array names no
        // group at all, so there is nothing for a request to match.
        Assert.False(PlanGroups.Matches(["reporting"], []));
        Assert.False(PlanGroups.Matches([], []));

        // ...and the same participant list under an empty request follows the single-group
        // rule per element: only the default group's name gets in.
        Assert.True(PlanGroups.Matches([], ["reporting", GroupAttribute.DefaultGroupName]));
    }
}
