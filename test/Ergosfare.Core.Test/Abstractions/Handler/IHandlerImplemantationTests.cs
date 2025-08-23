
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Abstractions.Handler;

public class IHandlerImplemantationTests
{
    [Fact]
    [Trait("Category", "Coverage")]

    public async Task IHandlerShouldImplement()
    {
        var ct = CancellationToken.None;
        var values = new Dictionary<object, object?>();
        var ctx = new ErgosfareExecutionContext(values, ct);
        IHandler handler = new StubNonGenericHandler();

        await using (AmbientExecutionContext.CreateScope(ctx))
        {
            var result = handler.Handle(
                new StubNonGenericMessage(), AmbientExecutionContext.Current);
            Assert.NotNull(result); 
            await Assert.IsType<Task>(result, exactMatch: false);
        }
    }
}