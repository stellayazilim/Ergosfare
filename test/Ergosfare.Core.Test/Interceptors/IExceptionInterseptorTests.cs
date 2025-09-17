using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Interceptors;

public class ExceptionInterceptorTests
{


    [Fact]
    [Trait("Category", "Coverage")]
    public async Task IExceptionInterceptorImplements()
    {
        var fixture = new ExecutionContextFixture();
        IExceptionInterceptor interceptor = new StubVoidAsyncExceptionInterceptor();
        
        
        var result = interceptor.Handle(new StubMessage(), "not null", new Exception(), fixture.Ctx);
        
        Assert.NotNull(result);
        await Assert.IsType<Task>(result, exactMatch: false);
    }
}