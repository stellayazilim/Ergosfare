using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;


/// <summary>
/// Contains unit tests for <see cref="IPostInterceptor"/> implementations,
/// verifying that the Handle method correctly returns a <see cref="Task"/> for asynchronous execution.
/// </summary>
public class PostInterceptorTests
{
    /// <summary>
    /// Tests that a post-interceptor returns a non-null <see cref="Task"/> 
    /// when handling a message with a result.
    /// </summary>
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