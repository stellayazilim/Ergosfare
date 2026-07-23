using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;


/// <summary>
/// Contains unit tests for <see cref="IAsyncPostInterceptor{TMessage}"/> implementations,
/// verifying that the typed <c>HandleAsync</c> member flows the pipeline result through.
/// </summary>
public class PostInterceptorTests
{
    /// <summary>
    /// Tests that an asynchronous post-interceptor invoked through its typed contract
    /// returns the (unmodified) pipeline result.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPostInterceptorsShouldImplement()
    {
        var fixture = new ExecutionContextFixture();
        IAsyncPostInterceptor<StubMessage> interceptor = new StubVoidAsyncPostInterceptor();

        var result = await interceptor.HandleAsync(new StubMessage(), StubPostInterceptor.Result, fixture.Ctx);

        Assert.Equal(StubPostInterceptor.Result, result);
    }
}
