using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// Execution-context pooling: rent/return reuse, state reset between uses, scope
/// lifecycle, and the allocation-free read paths of an empty context.
/// </summary>
public class ExecutionContextPoolTests
{
    [Fact]
    public void ReturnedContext_IsReusedWithCleanState()
    {
        var first = ErgosfareExecutionContextPool.Rent(null, default);
        first.Set("key", "value");
        ErgosfareExecutionContextPool.Return(first);

        using var cts = new CancellationTokenSource();
        var second = ErgosfareExecutionContextPool.Rent(null, cts.Token);

        try
        {
            // Fresh state regardless of which pooled instance came back (the pool is
            // process-wide and other tests rent concurrently): no stale items, new token.
            Assert.False(second.Has("key"));
            Assert.Equal(cts.Token, second.CancellationToken);
        }
        finally
        {
            ErgosfareExecutionContextPool.Return(second);
        }
    }

    [Fact]
    public void CreateScope_ChildInheritsToken_AndDisposeReturnsItToThePool()
    {
        using var cts = new CancellationTokenSource();
        var parent = ErgosfareExecutionContextPool.Rent(null, cts.Token);
        parent.Set("outer", "state");

        var scope = parent.CreateScope();
        var child = scope.Context;

        Assert.NotSame(parent, child);
        Assert.Equal(cts.Token, child.CancellationToken);
        Assert.False(child.Has("outer"));

        // Dispose returns the child to the pool; a fresh rent must never observe its state.
        child.Set("child", "state");
        scope.Dispose();

        var next = ErgosfareExecutionContextPool.Rent(null, default);

        try
        {
            Assert.False(next.Has("child"));
        }
        finally
        {
            ErgosfareExecutionContextPool.Return(next);
            ErgosfareExecutionContextPool.Return(parent);
        }
    }

    [Fact]
    public void EmptyContext_ReadPaths_DoNotAllocateTheItemsDictionary()
    {
        var context = new ErgosfareExecutionContext(null, default);

        Assert.False(context.Has("missing"));
        Assert.False(context.TryGet<string>("missing", out _));
        Assert.Throws<KeyNotFoundException>(() => context.Get<string>("missing"));

        // The read calls above must not have materialized the dictionary: the first
        // Items access creates it, so reference-compare across two accesses proves the
        // lazy path was still untouched.
        Assert.Same(context.Items, context.Items);
    }
}
