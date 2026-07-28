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
        public ValueTask HandleAsync(GroupedEvent @event, IExecutionContext context)
        {
            context.Set("auditRan", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class DefaultGroupHandler : IEventHandler<GroupedEvent>
    {
        public ValueTask HandleAsync(GroupedEvent @event, IExecutionContext context)
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

        var settings = new EventMediationSettings();
        await mediator.PublishAsync(new GroupedEvent(), settings);

        // A group-less publish serves the default group only.
        Assert.Equal(true, settings.Items["defaultRan"]);
        Assert.False(settings.Items.ContainsKey("auditRan"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithGroups_TakesTheMediateFallback_AndFiltersHandlers()
    {
        var provider = Build();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        var settings = new EventMediationSettings();
        settings.Filters.Groups = ["audit"];

        await mediator.PublishAsync(new GroupedEvent(), settings);

        // The grouped publish leaves the fast lane; only the requested group's handler runs.
        Assert.Equal(true, settings.Items["auditRan"]);
        Assert.False(settings.Items.ContainsKey("defaultRan"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task BothConstructors_PublishIdentically()
    {
        var provider = Build();
        await using var _ = provider;

        var strategy = provider.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>();
        var adapters = provider.GetRequiredService<IResultAdapterService>();

        var engineBacked = new EventMediator(
            provider.GetRequiredService<MessageDispatchEngine>(), provider, strategy, adapters);
        var mediatorBacked = new EventMediator(
            strategy, adapters, provider.GetRequiredService<IMessageMediator>());

        foreach (var mediator in new EventMediator[] { engineBacked, mediatorBacked })
        {
            var settings = new EventMediationSettings();

            await mediator.PublishAsync(new GroupedEvent(), settings);

            Assert.Equal(true, settings.Items["defaultRan"]);
            Assert.False(settings.Items.ContainsKey("auditRan"));
        }
    }
}
