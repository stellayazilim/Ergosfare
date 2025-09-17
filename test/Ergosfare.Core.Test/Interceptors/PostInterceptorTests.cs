using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Interceptors;

public class PostInterceptorTests
{
    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPostInterceptorsShouldImplement()
    {
     
        var fixture = new ExecutionContextFixture().PropagateAmbientContext();
        IPostInterceptor interceptor = new StubVoidAsyncPostInterceptor();

        var result =  interceptor.Handle(new StubMessage(), StubPostInterceptor.Result,  fixture.Ctx);

        Assert.NotNull(result);
    
        await Assert.IsType<Task>(result, exactMatch:false);

    }
}