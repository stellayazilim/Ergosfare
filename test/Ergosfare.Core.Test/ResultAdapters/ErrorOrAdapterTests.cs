using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Test.__fixtures__;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test.ResultAdapters;

public class ErrorOrAdapterTests
(ITestOutputHelper testOutputHelper)
{


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ErrorOrResultAdapterTests()
    {
    
       var ctx = ExecutionContextFixture.CreateExecutionContext();
       var errorOrAdapter = new ResultAdapterFixtures.ErrorOrAdapter();
       
       var handler = new ResultAdapterFixtures.ResultAdapterErrorOrHandlerReturnException();

       var result  = (object) await handler.HandleAsync(new ResultAdapterFixtures.ResultAdapterMessage(), ctx);

       testOutputHelper.WriteLine(result.ToString());
       testOutputHelper.WriteLine(errorOrAdapter.CanAdapt(result).ToString());
       // var canAdapt = errorOrAdapter.CanAdapt((object)result);
       // var isException = errorOrAdapter.TryGetException(result, out var exception);
       //
       // Assert.True(canAdapt);
       // Assert.True(isException);
       // Assert.NotNull(exception);
       // Assert.IsType<AdaptedException>(exception, exactMatch: false);
       // Assert.Single(result.Errors);
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public async Task ErrorOrResultAdapterShouldInvokeExceptionHandler()
    {
        // arrange
        var (services, mediator, _) = ResultAdapterFixtures.CreateFixture(
            [new ResultAdapterFixtures.ErrorOrAdapter()], 
            [
                typeof(ResultAdapterFixtures.ResultAdapterErrorOrHandlerReturnException),
                typeof(ResultAdapterFixtures.ResultAdapterExceptionInterceptor)
            ]);

        var strategy = ResultAdapterFixtures.GetStrategy(services);

        // act & assert
        var result = await mediator.Mediate(new ResultAdapterFixtures.ResultAdapterMessage(), strategy);
        Assert.NotEmpty(result.Errors); // ensures underlying ErrorOr produced errors

    }
}