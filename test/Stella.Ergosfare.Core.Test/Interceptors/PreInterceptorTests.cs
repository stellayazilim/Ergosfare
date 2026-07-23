using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;


/// <summary>
/// Contains unit tests for <see cref="IAsyncPreInterceptor{TMessage}"/> implementations,
/// verifying that the typed <c>HandleAsync</c> member produces the expected message.
/// </summary>
public class PreInterceptorTests
{
    /// <summary>
    /// Tests that an asynchronous pre-interceptor invoked through its typed contract
    /// returns the message that continues through the pipeline.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPreInterceptorsShouldImplement()
    {
        // arrange
        IAsyncPreInterceptor<StubMessage> interceptor = new StubVoidAsyncPreInterceptor();
        var fixture = new ExecutionContextFixture();
        var message = new StubMessage();

        // act
        var result = await interceptor.HandleAsync(message, fixture.Ctx);

        // assert
        Assert.Same(message, result);
    }
}
