using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Interceptors;

public class FinalInterceptorTests
{

   

    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task  TestFinalInterceptorTMessageShouldImplement()
    {
        var fixture = new ExecutionContextFixture();
        IFinalInterceptor interceptor = new StubVoidAsyncFinalInterceptor();

        var result =  interceptor.Handle(new  StubMessage(), null, null, fixture.Ctx);

        Assert.NotNull(result);
        await Assert.IsType<Task>(result, exactMatch:false);

    }

    
    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task  TestFinalInterceptorTMessageTResultShouldImplement()
    {
        var fixture = new ExecutionContextFixture();
        IFinalInterceptor interceptor = new StubStringAsyncFinalInterceptor();

        var result =  interceptor.Handle(new  StubMessage(), StubStringAsyncFinalInterceptor.Result, null, fixture.Ctx);

        Assert.NotNull(result);
        
        await Assert.IsType<Task>(result, exactMatch:false);

    }

}