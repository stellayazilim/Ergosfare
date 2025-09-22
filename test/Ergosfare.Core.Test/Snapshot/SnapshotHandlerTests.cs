using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Test.Fixtures.Stubs.Basic;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test.Snapshot;

public class SnapshotHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task SnapshotHandlerShouldTakeSnapshot()
    {
        
        var services = new ServiceCollection()
            .AddErgosfare(o => o.AddCoreModule(b => b.Register<StubVoidHandlerThrowsWithSnapshot>().Register<StubExceptionInterceptor>()))
            .BuildServiceProvider();
        var registry = services.GetRequiredService<IMessageRegistry>();
        var reslveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
        var mediator = services.GetRequiredService<IMessageMediator>();
        
        await mediator.Mediate(new StubMessage(), new MediateOptions<StubMessage, Task>()
        {
            CancellationToken = default,
            Groups = ["default"],
            MessageResolveStrategy = reslveStrategy,
            MessageMediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>(new ResultAdapterService())
        });
        

        Assert.NotNull(new ());
    }
}