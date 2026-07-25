using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;

/// <summary>
/// Contains unit tests for <see cref="IAsyncExceptionInterceptor{TMessage}"/> implementations,
/// verifying that the typed <c>HandleAsync</c> member flows the pipeline result through.
/// </summary>
public class ExceptionInterceptorTests
{
    /// <summary>
    /// Tests that an asynchronous exception interceptor invoked through its typed contract
    /// returns the (unmodified) pipeline result.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task IExceptionInterceptorImplements()
    {
        var fixture = new ExecutionContextFixture();
        IAsyncExceptionInterceptor<StubMessage> interceptor = new StubVoidAsyncExceptionInterceptor();

        var result = await interceptor.HandleAsync(new StubMessage(), "not null", new Exception(), fixture.Ctx);

        Assert.Equal("not null", result);
    }
}
