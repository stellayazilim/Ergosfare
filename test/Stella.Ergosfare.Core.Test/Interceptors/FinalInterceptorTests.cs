using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;

/// <summary>
/// Contains unit tests for <see cref="IFinalInterceptor"/> implementations,
/// verifying that the Handle methods correctly return <see cref="Task"/> for asynchronous execution.
/// </summary>
public class FinalInterceptorTests
{
    /// <summary>
    /// Tests that a final interceptor handling a message without a result returns a non-null <see cref="Task"/>.
    /// </summary>
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

    
    /// <summary>
    /// Tests that a final interceptor handling a message with a typed result returns a non-null <see cref="Task"/>.
    /// </summary>
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