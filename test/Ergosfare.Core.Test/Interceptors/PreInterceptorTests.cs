

using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Interceptors;

public class PreInterceptorTests
{
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPostInterceptorsShouldImplement()
    {
        // arrange 
        IPreInterceptor interceptor = new StubVoidAsyncPreInterceptor();
        var fixture = new ExecutionContextFixture();
        var message = new StubMessage();
        
        // act
        var result = interceptor.Handle(message, fixture.Ctx);

        // assert
        Assert.NotNull(result);
        await Assert.IsType<Task<object>>(result);
        var awaitedResult = await (Task<object>)result;
        Assert.IsType<StubMessage>(awaitedResult, exactMatch: false );
    }
}