using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// The <see cref="GroupSet"/> publish overloads: filtering through the typed and the
/// interface-erased shape, the canonical slot fast path under repetition and alternation,
/// and the empty set publishing the default pipeline. Helper types are excluded from
/// discovery; handlers record into a static slot, safe because the types are private to
/// this class and tests within a class run sequentially.
/// </summary>
public class GroupSetPublishTests
{
    private static readonly GroupSet Audit = GroupSet.Of("gsp.audit");
    private static readonly GroupSet Billing = GroupSet.Of("gsp.billing");

    private static string? _lastRan;

    [ExcludeFromDiscovery]
    public sealed class SlottedEvent : IEvent { }

    [ExcludeFromDiscovery]
    [Group("gsp.audit")]
    public sealed class AuditSlottedHandler : IEventHandler<SlottedEvent>
    {
        public ValueTask HandleAsync(SlottedEvent @event, ErgosfareContext context)
        {
            _lastRan = "audit";
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    [Group("gsp.billing")]
    public sealed class BillingSlottedHandler : IEventHandler<SlottedEvent>
    {
        public ValueTask HandleAsync(SlottedEvent @event, ErgosfareContext context)
        {
            _lastRan = "billing";
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class DefaultEvent : IEvent { }

    [ExcludeFromDiscovery]
    public sealed class DefaultEventHandler : IEventHandler<DefaultEvent>
    {
        public ValueTask HandleAsync(DefaultEvent @event, ErgosfareContext context)
        {
            _lastRan = "default";
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<AuditSlottedHandler>();
                e.Register<BillingSlottedHandler>();
                e.Register<DefaultEventHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedOverload_FiltersAndSurvivesRepetitionAndAlternation()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        _lastRan = null;
        await mediator.PublishAsync(new SlottedEvent(), Audit);
        Assert.Equal("audit", _lastRan);

        await mediator.PublishAsync(new SlottedEvent(), Audit);
        Assert.Equal("audit", _lastRan);

        await mediator.PublishAsync(new SlottedEvent(), Billing);
        Assert.Equal("billing", _lastRan);

        await mediator.PublishAsync(new SlottedEvent(), Audit);
        Assert.Equal("audit", _lastRan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ErasedOverload_ResolvesByRuntimeType_AndFilters()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        _lastRan = null;
        await mediator.PublishAsync((IEvent)new SlottedEvent(), Billing);

        Assert.Equal("billing", _lastRan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task EmptySet_PublishesTheDefaultPipeline()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        _lastRan = null;
        await mediator.PublishAsync(new DefaultEvent(), GroupSet.Empty);

        Assert.Equal("default", _lastRan);
    }
}
