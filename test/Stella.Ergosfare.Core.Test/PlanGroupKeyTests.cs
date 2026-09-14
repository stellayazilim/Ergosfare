using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Test;

public class PlanGroupKeyTests
{
    [Fact]
    public void OrderAndDuplicates_DoNotChangeThePlanKey()
    {
        var key = new PlanGroupKey(["reporting", "auditing"]);
        var same = new PlanGroupKey(["auditing", "reporting", "auditing"]);
        Assert.Equal(key, same);
        Assert.Equal(key.GetHashCode(), same.GetHashCode());
        Assert.True(key.Equals((object)same));
        Assert.False(key.Equals("not a key"));
    }

    [Fact]
    public void ContentAndCaseRemainSignificant()
    {
        var key = new PlanGroupKey(["a", "b"]);
        Assert.NotEqual(key, new PlanGroupKey(["a"]));
        Assert.NotEqual(key, new PlanGroupKey(["a", "c"]));
        Assert.NotEqual(key, new PlanGroupKey(["A", "b"]));
        Assert.NotEqual(key, new PlanGroupKey(["a\u001fb"]));
    }

    [Fact]
    public void EmptyAndFilteringKeysCannotCollideWithNamedGroups()
    {
        Assert.Equal(PlanGroupKey.Default, new PlanGroupKey([]));
        Assert.NotEqual(PlanGroupKey.Default, new PlanGroupKey([""]));
        Assert.NotEqual(PlanGroupKey.Filtering, new PlanGroupKey(["\0filtered"]));
    }

    [Fact]
    public void LookingUpExistingNames_DoesNotAllocate()
    {
        string[] names = ["b", "a", "b"];
        var table = new Dictionary<PlanGroupKey, int> { [new PlanGroupKey(["a", "b"])] = 42 };
        _ = table[new PlanGroupKey(names)];
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var i = 0; i < 100; i++) total += table[new PlanGroupKey(names)];
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(4200, total);
        Assert.Equal(0, allocated);
    }
}
