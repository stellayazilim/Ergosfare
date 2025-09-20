using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Interceptors;

/// <summary>
/// Contains unit tests for <see cref="IExceptionInterceptor"/> implementations,
/// verifying that the Handle method correctly returns a <see cref="Task"/> for asynchronous execution.
/// </summary>
public class ExceptionInterceptorTests
{
    
    /// <summary>
    /// Tests that an exception interceptor returns a non-null <see cref="Task"/> 
    /// when handling a message with a result and an exception.
    /// </summary>
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