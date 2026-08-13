using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Covers the engine-backed facade shape for events: DI resolves a single-object facade
/// bound to the process-wide <see cref="MessageDispatchEngine"/>, both public constructors
/// publish identically, and grouped publishes leave the fast lane for the original
/// Mediate path with its handler-group filtering intact.
/// </summary>
public class EngineBackedEventFacadeTests
{
    public sealed class GroupedEvent : IEvent { }

    /// <summary>
    /// Excluded from discovery so another test's assembly scan (the registry is
    /// process-wide) cannot register it into a group set this class does not control.
    /// </summary>
    [ExcludeFromDiscovery]
    [Group("audit")]
    public sealed class AuditGroupHandler : IEventHandler<GroupedEvent>
    {
        public ValueTask HandleAsync(GroupedEvent @event, ErgosfareContext context)
        {
            context.Set("auditRan", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class DefaultGroupHandler : IEventHandler<GroupedEvent>
    {
        public ValueTask HandleAsync(GroupedEvent @event, ErgosfareContext context)
        {
            context.Set("defaultRan", true);
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<AuditGroupHandler>();
                e.Register<DefaultGroupHandler>();
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
        await mediator.PublishAsync(new GroupedEvent(), settings);

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
        string[] groupFilter = ["audit"];

        await mediator.PublishAsync(new GroupedEvent(), items, groupFilter);

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

            await mediator.PublishAsync(new GroupedEvent(), settings);

            Assert.Equal(true, settings.Items["defaultRan"]);
            Assert.False(settings.Items.ContainsKey("auditRan"));
        }
    }
}
