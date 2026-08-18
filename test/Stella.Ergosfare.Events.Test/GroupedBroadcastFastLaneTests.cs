using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator compiles the plans grouped
// publishes run — the group sets below reach the dispatch as runtime values, which is what
// the group-filtering plan exists for. Every type is owned by GroupedBroadcastFastLaneTests
// alone, and each container registers its events' full compiled pipelines.

public sealed class LaneEvent : IEvent { }

[Group("lane.alpha")]
public sealed class LaneAlphaHandler : IEventHandler<LaneEvent>
{
    public ValueTask HandleAsync(LaneEvent @event, ErgosfareContext context)
    {
        context.Set("alphaRan", true);
        return ValueTask.CompletedTask;
    }
}

[Group("lane.beta")]
public sealed class LaneBetaHandler : IEventHandler<LaneEvent>
{
    public ValueTask HandleAsync(LaneEvent @event, ErgosfareContext context)
    {
        context.Set("betaRan", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class InterceptedLaneEvent : IEvent { }

[Group("lane.guarded")]
public sealed class LaneGuardedHandler : IEventHandler<InterceptedLaneEvent>
{
    public ValueTask HandleAsync(InterceptedLaneEvent @event, ErgosfareContext context)
    {
        context.Set("guardedRan", true);
        return ValueTask.CompletedTask;
    }
}

[Group("lane.guarded")]
public sealed class LaneGuardedInterceptor : IEventPreInterceptor<InterceptedLaneEvent>
{
    public ValueTask<InterceptedLaneEvent> HandleAsync(InterceptedLaneEvent @event, ErgosfareContext context)
    {
        context.Set("guardedInterceptorRan", true);
        return ValueTask.FromResult(@event);
    }
}

/// <summary>
/// Grouped publishes whose group set is a runtime value: they run through the compiled
/// group-filtering plan, served from a last-used (group set) slot. The tests stress the
/// slot's weak spots — alternating group sets, a reused settings instance whose group list
/// is mutated in place, and a grouped pipeline that carries an interceptor (which must run
/// the interceptor before the handler).
/// </summary>
public class GroupedBroadcastFastLaneTests
{
    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<LaneAlphaHandler>();
                e.Register<LaneBetaHandler>();
            }))
            .BuildServiceProvider();

    private static async Task<ErgosfareContext> Publish(IEventMediator mediator, params string[] groups)
    {
        var settings = new ErgosfareContext();
        var groupFilter = groups;

        await mediator.PublishAsync(new LaneEvent(), settings, groupFilter);

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
        var first = await Publish(mediator, "lane.alpha");
        Assert.Equal(true, first.Items["alphaRan"]);
        Assert.False(first.Items.ContainsKey("betaRan"));

        var second = await Publish(mediator, "lane.beta");
        Assert.Equal(true, second.Items["betaRan"]);
        Assert.False(second.Items.ContainsKey("alphaRan"));

        var third = await Publish(mediator, "lane.alpha");
        Assert.Equal(true, third.Items["alphaRan"]);
        Assert.False(third.Items.ContainsKey("betaRan"));
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
        var groups = new List<string> { "lane.alpha" };
        var settings = new ErgosfareContext();
        var groupFilter = groups;

        await mediator.PublishAsync(new LaneEvent(), settings, groupFilter);
        Assert.Equal(true, settings.Items["alphaRan"]);

        groups.Clear();
        groups.Add("lane.beta");
        settings.Items.Clear();

        await mediator.PublishAsync(new LaneEvent(), settings, groupFilter);
        Assert.Equal(true, settings.Items["betaRan"]);
        Assert.False(settings.Items.ContainsKey("alphaRan"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupedPipelineWithInterceptor_RunsTheFullStrategy()
    {
        // Register the intercepted pipeline in its own container to keep the fixture
        // handlers' pipelines interceptor-free.
        await using var guarded = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<LaneGuardedHandler>();
                e.Register<LaneGuardedInterceptor>();
            }))
            .BuildServiceProvider();

        var mediator = guarded.GetRequiredService<IEventMediator>();

        var settings = new ErgosfareContext();
        string[] groupFilter = ["lane.guarded"];

        await mediator.PublishAsync(new InterceptedLaneEvent(), settings, groupFilter);

        // A grouped pipeline that carries an interceptor runs the interceptor before the
        // handler — the plan bakes both.
        Assert.Equal(true, settings.Items["guardedInterceptorRan"]);
        Assert.Equal(true, settings.Items["guardedRan"]);
    }
}
