using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// The grouped broadcast fast lane: grouped publishes resolve the same group-filtered
/// pipeline the Mediate path built, served from a last-used (factory, group set) plan
/// slot. The tests stress the slot's weak spots — alternating group sets, a reused
/// settings instance whose group list is mutated in place, and a grouped pipeline that
/// carries an interceptor (which must keep the strategy path, not the straight-through
/// loop).
/// </summary>
public class GroupedBroadcastFastLaneTests
{
    [ExcludeFromDiscovery]
    public sealed class LaneEvent : IEvent { }

    [ExcludeFromDiscovery]
    [Group("alpha")]
    public sealed class AlphaHandler : IEventHandler<LaneEvent>
    {
        public ValueTask HandleAsync(LaneEvent @event, ErgosfareContext context)
        {
            context.Set("alphaRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    [Group("beta")]
    public sealed class BetaHandler : IEventHandler<LaneEvent>
    {
        public ValueTask HandleAsync(LaneEvent @event, ErgosfareContext context)
        {
            context.Set("betaRan", true);
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<AlphaHandler>();
                e.Register<BetaHandler>();
            }))
            .BuildServiceProvider();

    private static async Task<IDictionary<object, object?>> Publish(IEventMediator mediator, params string[] groups)
    {
        var settings = new Dictionary<object, object?>();
        var groupFilter = groups;

        await mediator.PublishAsync(new LaneEvent(), groupFilter, settings, false, CancellationToken.None);

        return settings;
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AlternatingGroupSets_AlwaysRunTheRequestedGroup()
    {
        var provider = Build();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        // alpha populates the slot, beta must not be served alpha's plan, and alpha
        // again must survive the slot having moved on.
        var first = await Publish(mediator, "alpha");
        Assert.Equal(true, first["alphaRan"]);
        Assert.False(first.ContainsKey("betaRan"));

        var second = await Publish(mediator, "beta");
        Assert.Equal(true, second["betaRan"]);
        Assert.False(second.ContainsKey("alphaRan"));

        var third = await Publish(mediator, "alpha");
        Assert.Equal(true, third["alphaRan"]);
        Assert.False(third.ContainsKey("betaRan"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MutatedReusedGroupList_IsSeenAsANewGroupSet()
    {
        var provider = Build();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        // One settings instance, one List instance — mutated between publishes. The slot
        // snapshots group contents, so the second publish must re-resolve, not replay
        // alpha's plan.
        var groups = new List<string> { "alpha" };
        var settings = new Dictionary<object, object?>();
        var groupFilter = groups;

        await mediator.PublishAsync(new LaneEvent(), groupFilter, settings, false, CancellationToken.None);
        Assert.Equal(true, settings["alphaRan"]);

        groups.Clear();
        groups.Add("beta");
        settings.Clear();

        await mediator.PublishAsync(new LaneEvent(), groupFilter, settings, false, CancellationToken.None);
        Assert.Equal(true, settings["betaRan"]);
        Assert.False(settings.ContainsKey("alphaRan"));
    }

    [ExcludeFromDiscovery]
    public sealed class InterceptedLaneEvent : IEvent { }

    [ExcludeFromDiscovery]
    [Group("guarded")]
    public sealed class GuardedHandler : IEventHandler<InterceptedLaneEvent>
    {
        public ValueTask HandleAsync(InterceptedLaneEvent @event, ErgosfareContext context)
        {
            context.Set("guardedRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    [Group("guarded")]
    public sealed class GuardedInterceptor : IEventPreInterceptor<InterceptedLaneEvent>
    {
        public ValueTask<InterceptedLaneEvent> HandleAsync(InterceptedLaneEvent @event, ErgosfareContext context)
        {
            context.Set("guardedInterceptorRan", true);
            return ValueTask.FromResult(@event);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupedPipelineWithInterceptor_RunsTheFullStrategy()
    {
        var provider = Build();
        await using var _ = provider;

        // Register the intercepted pipeline in its own container to keep the fixture
        // handlers' pipelines interceptor-free.
        await using var guarded = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<GuardedHandler>();
                e.Register<GuardedInterceptor>();
            }))
            .BuildServiceProvider();

        var mediator = guarded.GetRequiredService<IEventMediator>();

        var settings = new Dictionary<object, object?>();
        string[] groupFilter = ["guarded"];

        await mediator.PublishAsync(new InterceptedLaneEvent(), groupFilter, settings, false, CancellationToken.None);

        // A grouped pipeline that carries an interceptor must leave the straight-through
        // loop to the strategy, which runs the interceptor before the handler.
        Assert.Equal(true, settings["guardedInterceptorRan"]);
        Assert.Equal(true, settings["guardedRan"]);
    }
}
