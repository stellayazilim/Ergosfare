using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Test.__fixtures__;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Test.ResultAdapters;

public class ErrorOrAdapterTests
{


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ErrorOrResultAdapterTests()
    {
    
       var ctx = ExecutionContextFixture.CreateExecutionContext();
       var errorOrAdapter = new ResultAdapterFixtures.ErrorOrAdapter();
       
       var handler = new ResultAdapterFixtures.ResultAdapterErrorOrHandler();

       var result  = await handler.HandleAsync(new ResultAdapterFixtures.ResultAdapterMessage(), ctx);

       var canAdapt = errorOrAdapter.CanAdapt(result);
       var isException = errorOrAdapter.TryGetException(result, out var exception);
       
       Assert.True(canAdapt);
       Assert.True(isException);
       Assert.NotNull(exception);
       Assert.IsType<AdaptedException>(exception, exactMatch: false);
       Assert.Single(result.Errors);
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public async Task ErrorOrResultAdapterShouldInvokeExceptionHandler()
    {
        // arrange
        var (services, mediator, registry) = ResultAdapterFixtures.CreateFixture(
            [new ResultAdapterFixtures.ErrorOrAdapter()], 
            [
                typeof(ResultAdapterFixtures.ResultAdapterErrorOrHandler),
                typeof(ResultAdapterFixtures.ResultAdapterExceptionInterceptor)
            ]);

        var strategy = ResultAdapterFixtures.GetStrategy(services);

        // act & assert
        var exception = await Assert.ThrowsAsync<AdaptedException>(async () =>
        {
            var result = await mediator.Mediate(new ResultAdapterFixtures.ResultAdapterMessage(), strategy);
            Assert.NotEmpty(result.Errors); // ensures underlying ErrorOr produced errors
        });

        // verify
        Assert.NotNull(exception);
        Assert.True(ResultAdapterFixtures.ResultAdapterExceptionInterceptor.IsCalled);
        Assert.IsAssignableFrom<IErrorOr>(exception.OriginalResult); // optional: check we preserved original
    }
}