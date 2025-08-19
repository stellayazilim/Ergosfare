using Ergosfare.Core.Context;
using ExecutionContext = Ergosfare.Core.Internal.Contexts.ExecutionContext;

namespace Ergosfare.Core.Test;

public class AmbientExecutionContextTests
{


    [Fact]
    [Trait("Category", "Coverage")]
    public void AmbientExecutionContextShouldHasNoCurrent()
    { 
        var ctx = new ExecutionContext(
            CancellationToken.None, new Dictionary<object, object?>());
        // arrange
        Assert.False(AmbientExecutionContext.HasCurrent);
        
        AmbientExecutionContext.Current = ctx;
        
        Assert.True(AmbientExecutionContext.HasCurrent);
        Assert.Same(ctx, AmbientExecutionContext.Current);
    }


    [Fact]
    [Trait("Category", "Coverage")]
    public void AmbientExecutionContextShouldHaveCurrent()
    {
        var ctx = new ExecutionContext(
            CancellationToken.None, new Dictionary<object, object?>());

        var ctx2 = new ExecutionContext(
            CancellationToken.None, new Dictionary<object, object?>());
        AmbientExecutionContext.CreateScope(ctx);
        
        Assert.True(AmbientExecutionContext.HasCurrent);
        Assert.Same(ctx, AmbientExecutionContext.Current);
        Assert.NotSame(ctx2, AmbientExecutionContext.Current);
    }


    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task AmbientExecutionContextShouldBeDisposable()
    {
        
        // arrange
        var ctx = new ExecutionContext(
            CancellationToken.None, new Dictionary<object, object?>());
        
        var scope = AmbientExecutionContext.CreateScope(ctx);
        
        // act
        scope.Dispose();
        await scope.DisposeAsync();
    
        // assert
        Assert.False(AmbientExecutionContext.HasCurrent);
    }
}