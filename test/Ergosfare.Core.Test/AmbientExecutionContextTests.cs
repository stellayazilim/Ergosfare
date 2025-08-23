using Ergosfare.Context;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class AmbientExecutionContextTests
{
    [Fact]
    [Trait("Category", "Coverage")]
    public void AmbientExecutionContextShouldHasNoCurrent()
    {
        var ctx = StubExecutionContext.Create();
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
        var ctx = StubExecutionContext.Create();

        var ctx2 = StubExecutionContext.Create();
        
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
        var ctx =  StubExecutionContext.Create();
        var scope = AmbientExecutionContext.CreateScope(ctx);
        
        // act
        scope.Dispose();
        await scope.DisposeAsync();
    
        // assert
        Assert.False(AmbientExecutionContext.HasCurrent);
    }
}