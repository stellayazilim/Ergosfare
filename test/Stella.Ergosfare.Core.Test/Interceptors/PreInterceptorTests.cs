using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Interceptors;


/// <summary>
/// Contains unit tests for <see cref="IPreInterceptor"/> implementations,
/// verifying that the Handle method correctly returns a <see cref="Task"/> and produces the expected result.
/// </summary>
public class PreInterceptorTests
{
    /// <summary>
    /// Tests that a pre-interceptor returns a non-null <see cref="Task{object}"/> 
    /// and that the
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPostInterceptorsShouldImplement()
    {
        // arrange 
        IPreInterceptor interceptor = new StubVoidAsyncPreInterceptor();
        var fixture = new ExecutionContextFixture();
        var message = new StubMessage();
        
        // act
        var result = interceptor.Handle(message, fixture.Ctx);

        // assert
        Assert.NotNull(result);
        await Assert.IsType<Task<object>>(result);
        var awaitedResult = await (Task<object>)result;
        Assert.IsType<StubMessage>(awaitedResult, exactMatch: false );
    }
}