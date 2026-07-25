using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;

/// <summary>
/// Contains unit tests for <see cref="IAsyncFinalInterceptor{TMessage}"/> and
/// <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/> implementations,
/// verifying that the typed <c>HandleAsync</c> members complete.
/// </summary>
public class FinalInterceptorTests
{
    /// <summary>
    /// Tests that a result-agnostic asynchronous final interceptor invoked through its typed
    /// contract completes without error.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestFinalInterceptorTMessageShouldImplement()
    {
        var fixture = new ExecutionContextFixture();
        IAsyncFinalInterceptor<StubMessage> interceptor = new StubVoidAsyncFinalInterceptor();

        await interceptor.HandleAsync(new StubMessage(), null, null, fixture.Ctx);
    }


    /// <summary>
    /// Tests that a result-typed asynchronous final interceptor invoked through its typed
    /// contract completes without error.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestFinalInterceptorTMessageTResultShouldImplement()
    {
        var fixture = new ExecutionContextFixture();
        IAsyncFinalInterceptor<StubMessage, string> interceptor = new StubStringAsyncFinalInterceptor();

        await interceptor.HandleAsync(new StubMessage(), StubStringAsyncFinalInterceptor.Result, null, fixture.Ctx);
    }
}
