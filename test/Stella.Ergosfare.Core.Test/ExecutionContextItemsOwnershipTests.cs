using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// Unit tests for the items-dictionary ownership rules of
/// <see cref="ErgosfareExecutionContext"/>: a dictionary adopted from the caller (a
/// settings object) must survive the context's return to the pool untouched, while the
/// lazily created (owned) dictionary is cleared and kept for capacity reuse — and one
/// dispatch's items must never leak into the next dispatch that rents the same context.
/// </summary>
public class ExecutionContextItemsOwnershipTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Return_ShouldDetachAdoptedDictionary_WithoutClearingIt()
    {
        var callerItems = new Dictionary<object, object?> { ["seed"] = "value" };

        var context = ErgosfareExecutionContextPool.Rent(callerItems, CancellationToken.None);
        context.Set("writtenByHandler", 42);

        ErgosfareExecutionContextPool.Return(context);

        // The caller keeps both its own seed and what handlers wrote during the dispatch.
        Assert.Equal("value", callerItems["seed"]);
        Assert.Equal(42, callerItems["writtenByHandler"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Rent_AfterAdoptedDispatch_ShouldNotExposePreviousCallersItems()
    {
        var callerItems = new Dictionary<object, object?> { ["secret"] = "data" };

        var first = ErgosfareExecutionContextPool.Rent(callerItems, CancellationToken.None);
        ErgosfareExecutionContextPool.Return(first);

        // The same pooled instance comes back on this thread; it must not carry the
        // previous caller's dictionary.
        var second = ErgosfareExecutionContextPool.Rent(items: null, CancellationToken.None);

        Assert.False(second.Has("secret"));

        ErgosfareExecutionContextPool.Return(second);

        // And writing through the second context must never reach the first caller.
        Assert.False(callerItems.ContainsKey("lateWrite"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Return_ShouldClearOwnedDictionary_AndReuseItAcrossRents()
    {
        var first = ErgosfareExecutionContextPool.Rent(items: null, CancellationToken.None);
        first.Set("transient", "state");
        ErgosfareExecutionContextPool.Return(first);

        var second = ErgosfareExecutionContextPool.Rent(items: null, CancellationToken.None);

        Assert.False(second.Has("transient"));

        ErgosfareExecutionContextPool.Return(second);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Reset_WithNewAdoptedDictionary_ShouldReplaceDetachedOne()
    {
        var firstCaller = new Dictionary<object, object?> { ["a"] = 1 };
        var secondCaller = new Dictionary<object, object?> { ["b"] = 2 };

        var context = ErgosfareExecutionContextPool.Rent(firstCaller, CancellationToken.None);
        ErgosfareExecutionContextPool.Return(context);

        var reused = ErgosfareExecutionContextPool.Rent(secondCaller, CancellationToken.None);

        Assert.False(reused.Has("a"));
        Assert.True(reused.Has("b"));

        reused.Set("c", 3);
        ErgosfareExecutionContextPool.Return(reused);

        Assert.False(firstCaller.ContainsKey("c"));
        Assert.Equal(3, secondCaller["c"]);
    }
}
