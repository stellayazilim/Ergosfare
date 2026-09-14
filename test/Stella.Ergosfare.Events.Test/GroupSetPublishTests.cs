using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator compiles the per-set plans
// the GroupSet call sites name; every type is owned by GroupSetPublishTests alone, and the
// container below registers each event's full compiled pipeline.

/// <summary>
/// What the handlers below observed, recorded per test; safe because tests within a class
/// run sequentially.
/// </summary>
internal static class GroupSetPublishProbe
{
    public static string? LastRan;
}

public sealed class GspSlottedEvent : IEvent { }

[Group("gsp.audit")]
public sealed class GspAuditSlottedHandler : IEventHandler<GspSlottedEvent>
{
    public ValueTask HandleAsync(GspSlottedEvent @event, ErgosfareContext context)
    {
        GroupSetPublishProbe.LastRan = "audit";
        return ValueTask.CompletedTask;
    }
}

[Group("gsp.billing")]
public sealed class GspBillingSlottedHandler : IEventHandler<GspSlottedEvent>
{
    public ValueTask HandleAsync(GspSlottedEvent @event, ErgosfareContext context)
    {
        GroupSetPublishProbe.LastRan = "billing";
        return ValueTask.CompletedTask;
    }
}

public sealed class GspDefaultEvent : IEvent { }

public sealed class GspDefaultEventHandler : IEventHandler<GspDefaultEvent>
{
    public ValueTask HandleAsync(GspDefaultEvent @event, ErgosfareContext context)
    {
        GroupSetPublishProbe.LastRan = "default";
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The <see cref="GroupSet"/> publish overloads: filtering through the typed and the
/// interface-erased shape, the canonical slot fast path under repetition and alternation,
/// and the empty set publishing the default pipeline.
/// </summary>
public class GroupSetPublishTests
{
    private static readonly GroupSet Audit = GroupSet.Of("gsp.audit");
    private static readonly GroupSet Billing = GroupSet.Of("gsp.billing");

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<GspAuditSlottedHandler>();
                e.Register<GspBillingSlottedHandler>();
                e.Register<GspDefaultEventHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedOverload_FiltersAndSurvivesRepetitionAndAlternation()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        GroupSetPublishProbe.LastRan = null;
        await mediator.PublishAsync(new GspSlottedEvent(), Audit);
        Assert.Equal("audit", GroupSetPublishProbe.LastRan);

        await mediator.PublishAsync(new GspSlottedEvent(), Audit);
        Assert.Equal("audit", GroupSetPublishProbe.LastRan);

        await mediator.PublishAsync(new GspSlottedEvent(), Billing);
        Assert.Equal("billing", GroupSetPublishProbe.LastRan);

        await mediator.PublishAsync(new GspSlottedEvent(), Audit);
        Assert.Equal("audit", GroupSetPublishProbe.LastRan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ErasedOverload_ResolvesByRuntimeType_AndFilters()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        GroupSetPublishProbe.LastRan = null;
        await mediator.PublishAsync((IEvent)new GspSlottedEvent(), Billing);

        Assert.Equal("billing", GroupSetPublishProbe.LastRan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task EmptySet_PublishesTheDefaultPipeline()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        GroupSetPublishProbe.LastRan = null;
        await mediator.PublishAsync(new GspDefaultEvent(), GroupSet.Empty);

        Assert.Equal("default", GroupSetPublishProbe.LastRan);
    }
}
