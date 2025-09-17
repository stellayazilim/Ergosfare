using Ergosfare.Context;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Test.Fixtures;

namespace Ergosfare.Core.Test;

/// <summary>
/// Unit tests for verifying the behavior of execution context handling,
/// ambient context scoping, and related exceptions.
/// </summary>
public class ExecutionContextTests
{
    
    /// <summary>
    /// Ensures that <see cref="ErgosfareExecutionContext"/> can be constructed,
    /// shares the same items dictionary reference, respects the cancellation token,
    /// and correctly sets the message result when aborted.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ExecutionContextShouldConstructed()
    {
        var token = CancellationToken.None;
        var items = new Dictionary<object, object?>();
        
        // arrange & act: create a new execution context
        var ctx = new ErgosfareExecutionContext(items, token);
        
        // modify dictionary after construction (should still reference the same instance)
        items.Add("foo", "bar");

        // act & assert: aborting should throw and set the MessageResult
        Assert.Throws<ExecutionAbortedException>(() => ctx.Abort("baz"));
        Assert.Equal(token, ctx.CancellationToken);
        Assert.Equal(items, ctx.Items);
        Assert.Equal("baz", ctx.MessageResult);
        
        
    }
    
    /// <summary>
    /// Verifies that <see cref="NoExecutionContextException"/> has the expected default message.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NoExecutionContextExceptionShouldHaveDefaultMessage()
    {
        // arrange
        var exception = new NoExecutionContextException();
        
        // act & assert
        Assert.Equal("No execution context is set", exception.Message);
        
    }

    /// <summary>
    /// Ensures that when an ambient execution context scope is disposed asynchronously,
    /// the previous context is restored properly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ScopedContextShouldDisposedAsync()
    {
        var fixture = new ExecutionContextFixture().PropagateAmbientContext();

        var ctx = fixture.CreateContext();

        // act: create a scope with the new context and dispose asynchronously
        await using (var _ = AmbientExecutionContext.CreateScope(ctx))
        {
            // just need to dispose; nothing else required
        }
        
        // assert: previous context is restored after disposal
        Assert.Same(fixture.Ctx, AmbientExecutionContext.Current);
    }
    
    
    /// <summary>
    /// Ensures that when an ambient execution context scope is disposed synchronously,
    /// the previous context is restored and double-disposal is safely handled.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ScopedContextShouldDisposed()
    {
        var fixture = new ExecutionContextFixture().PropagateAmbientContext();

        var ctx = fixture.CreateContext();

        // act: create a new scope
        var scope = AmbientExecutionContext.CreateScope(ctx);
        
        
        // assert in scope: current context should NOT be the fixture context
        Assert.NotSame(fixture.Ctx, AmbientExecutionContext.Current);
        
        // ReSharper disable once MethodHasAsyncOverload
        // disposing once should recover previous context
        scope.Dispose();
        
        // assert 2, should recovered
        Assert.Same(fixture.Ctx, AmbientExecutionContext.Current);
        
        // disposing twice should be safe and keep the previous context intact
        scope.Dispose();
        Assert.Same(fixture.Ctx, AmbientExecutionContext.Current);
    }
}