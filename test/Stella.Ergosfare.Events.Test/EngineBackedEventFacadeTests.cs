using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator compiles the default and
// per-set broadcast plans; every type is owned by EngineBackedEventFacadeTests alone, and
// the container below registers the event's full compiled pipeline.

public sealed class FacadeGroupedEvent : IEvent { }

[Group("facade.audit")]
public sealed class FacadeAuditGroupHandler : IEventHandler<FacadeGroupedEvent>
{
    public ValueTask HandleAsync(FacadeGroupedEvent @event, ErgosfareContext context)
    {
        context.Set("auditRan", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class FacadeDefaultGroupHandler : IEventHandler<FacadeGroupedEvent>
{
    public ValueTask HandleAsync(FacadeGroupedEvent @event, ErgosfareContext context)
    {
        context.Set("defaultRan", true);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Covers the engine-backed facade shape for events: DI resolves a single-object facade
/// bound to the process-wide <see cref="MessageDispatchEngine"/>, both public constructors
/// publish identically, and grouped publishes run the compiled per-set plan with its
/// handler-group filtering intact.
/// </summary>
public class EngineBackedEventFacadeTests
{
    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<FacadeAuditGroupHandler>();
                e.Register<FacadeDefaultGroupHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DiResolvedFacade_IsTheEngineBackedShape_AndPublishes()
    {
        var provider = Build();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        Assert.IsAssignableFrom<EventMediator>(mediator);
        Assert.NotEqual(typeof(EventMediator), mediator.GetType());

        var settings = new ErgosfareContext();
        await mediator.PublishAsync(new FacadeGroupedEvent(), settings);

        // A group-less publish serves the default group only.
        Assert.Equal(true, settings.Items["defaultRan"]);
        Assert.False(settings.Items.ContainsKey("auditRan"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithGroups_FiltersHandlers()
    {
        var provider = Build();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        var items = new ErgosfareContext();
        string[] groupFilter = ["facade.audit"];

        await mediator.PublishAsync(new FacadeGroupedEvent(), items, groupFilter);

        // The grouped publish runs the group-filtered plan; only the requested group's
        // handler runs.
        Assert.Equal(true, items.Items["auditRan"]);
        Assert.False(items.Items.ContainsKey("defaultRan"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ADirectlyConstructedFacade_PublishesLikeTheResolvedOne()
    {
        var provider = Build();
        await using var _ = provider;

        var constructed = new EventMediator(provider.GetRequiredService<MessageDispatchEngine>(), provider);

        foreach (var mediator in new [] { constructed, (EventMediator)provider.GetRequiredService<IEventMediator>() })
        {
            var settings = new ErgosfareContext();

            await mediator.PublishAsync(new FacadeGroupedEvent(), settings);

            Assert.Equal(true, settings.Items["defaultRan"]);
            Assert.False(settings.Items.ContainsKey("auditRan"));
        }
    }
}
