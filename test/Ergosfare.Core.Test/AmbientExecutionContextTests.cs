using Ergosfare.Context;
using Ergosfare.Test.Fixtures;
// ReSharper disable ConvertToPrimaryConstructor

namespace Ergosfare.Core.Test;

/// <summary>
/// Unit tests for <see cref="AmbientExecutionContext"/> using <see cref="ExecutionContextFixture"/>.
/// Demonstrates behavior of the ambient context, including existence, scoping, disposal, and nested context restoration.
/// </summary>
public class AmbientExecutionContextTests : IClassFixture<ExecutionContextFixture>
{
    private readonly ExecutionContextFixture _executionContextFixture;

    /// <summary>
    /// Initializes a new instance of <see cref="AmbientExecutionContextTests"/> with the provided fixture.
    /// </summary>
    /// <param name="executionContextFixture">The fixture providing execution context setup.</param>
    public AmbientExecutionContextTests(ExecutionContextFixture executionContextFixture)
    {
        _executionContextFixture = executionContextFixture;
    }

    /// <summary>
    /// Verifies that the ambient execution context has no current context by default.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void AmbientExecutionContextShouldHasNoCurrent()
    {
        Assert.False(AmbientExecutionContext.HasCurrent);
        Assert.Throws<NoExecutionContextException>( () => AmbientExecutionContext.Current);
    }

    /// <summary>
    /// Verifies that creating a scoped context temporarily sets <see cref="AmbientExecutionContext.Current"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task AmbientExecutionContextShouldHaveCurrent()
    {
        var ctx = _executionContextFixture.PropagateAmbientContext().CreateContext();
        
        await using var scope = _executionContextFixture.CreateScope(ctx);
        
        Assert.True(AmbientExecutionContext.HasCurrent);
        Assert.Same(ctx, AmbientExecutionContext.Current);
    }

    /// <summary>
    /// Verifies that a scoped ambient context is correctly disposed after leaving the scope.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task AmbientExecutionContextShouldBeDisposable()
    {
        await using (var scope = _executionContextFixture.CreateScope())
        {
            // Scoped context should exist
            Assert.True(AmbientExecutionContext.HasCurrent);
        }

        // Scope disposed, context should no longer exist
        Assert.False( AmbientExecutionContext.HasCurrent);
    }

    /// <summary>
    /// Verifies that nested scopes correctly restore the previous ambient context after disposal.
    /// Also ensures that items stored in the parent context are preserved.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task AmbientExecutionContextShouldHaveRestoredWhenScopeDisposed()
    {   
        var ctx = _executionContextFixture.PropagateAmbientContext().CreateContext();
        var ctx2 = _executionContextFixture.CreateContext();

        await using var _ = AmbientExecutionContext.CreateScope(ctx);
        
        // Sets an item in the parent context
        AmbientExecutionContext.Current.Items["item"] = "foo";
        
        await using (AmbientExecutionContext.CreateScope(ctx2))
        {
            // Scoped to ctx2: parent context item should not be present
            Assert.False(ctx2.Items.ContainsKey("item"));
            Assert.Same(ctx2, AmbientExecutionContext.Current);
        }
        
        // After disposing nested scope, parent context restored
        Assert.Same(ctx, AmbientExecutionContext.Current);
        Assert.Equal("foo", AmbientExecutionContext.Current.Items["item"]);
    }
}
