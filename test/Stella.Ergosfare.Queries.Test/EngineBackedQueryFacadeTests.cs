using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// Covers the engine-backed facade shape for queries: DI resolves a single-object facade
/// bound to the process-wide <see cref="MessageDispatchEngine"/>, both public constructors
/// query identically, and the streaming path — which stays on the mediator's
/// <c>Mediate(options)</c> machinery — resolves the scope's mediator on demand.
/// </summary>
public class EngineBackedQueryFacadeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DiResolvedFacade_IsTheEngineBackedShape_AndQueries()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStringResultQueryHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.IsAssignableFrom<QueryMediator>(mediator);
        Assert.NotEqual(typeof(QueryMediator), mediator.GetType());
        Assert.Equal(string.Empty, await mediator.QueryAsync(new StubNonGenericStringResultQuery()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DiResolvedFacade_Streams_ThroughTheEngineFastLane()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStreamStringResultQueryHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IQueryMediator>();

        var result = new List<string>();

        await foreach (var item in mediator.StreamAsync(new StubNonGenericStreamStringResultQuery()))
        {
            result.Add(item);
        }

        Assert.Equal(["Foo", "Bar", "Baz"], result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task BothConstructors_QueryIdentically()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStringResultQueryHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var strategy = provider.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>();

        var engineBacked = new QueryMediator(
            provider.GetRequiredService<MessageDispatchEngine>(), provider, strategy);
        var mediatorBacked = new QueryMediator(
            strategy, provider.GetRequiredService<IMessageMediator>());

        foreach (var mediator in new[] { engineBacked, mediatorBacked })
        {
            Assert.Equal(string.Empty, await mediator.QueryAsync(new StubNonGenericStringResultQuery()));
        }
    }
}
