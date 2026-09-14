using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator compiles the derived event's
// broadcast plan; owned by TypedPublishHolderTests alone.

public class HolderBaseEvent : IEvent { }

public sealed class HolderDerivedEvent : HolderBaseEvent { }

public sealed class HolderDerivedEventHandler : IEventHandler<HolderDerivedEvent>
{
    public ValueTask HandleAsync(HolderDerivedEvent @event, ErgosfareContext context)
    {
        context.Set("derivedRan", true);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Covers the static-generic slot behind the typed publish overload: it must serve the same
/// pipeline the runtime-type lookup does (one composition cache per message type), it must not
/// leak between containers, and a generic call made through a base-typed variable must keep
/// dispatching by the event's runtime type.
/// </summary>
public class TypedPublishHolderTests
{
    [Fact]
    public void TheBroadcastPlan_IsTheExecutor()
    {
        var plan = global::Stella.Ergosfare.Core.Abstractions.Planning.GeneratedPlanRegistry.FindBroadcastPlan(typeof(HolderDerivedEvent));
        Assert.IsAssignableFrom<IPipelineExecutor>(plan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ThroughABaseTypedVariable_DispatchesByRuntimeType()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<HolderDerivedEventHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        // The variable's static type closes the generic overload over HolderBaseEvent; the
        // holder guard must reject it and resolve the HolderDerivedEvent pipeline instead.
        HolderBaseEvent @event = new HolderDerivedEvent();
        var settings = new ErgosfareContext();

        await mediator.PublishAsync(@event, settings);

        Assert.Equal(true, settings.Items["derivedRan"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ThroughTheConcreteType_TakesTheHolderAndDispatches()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<HolderDerivedEventHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        var settings = new ErgosfareContext();

        await mediator.PublishAsync(new HolderDerivedEvent(), settings);

        Assert.Equal(true, settings.Items["derivedRan"]);
    }
}
