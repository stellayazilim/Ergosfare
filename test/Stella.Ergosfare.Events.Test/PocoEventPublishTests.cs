using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// Top level, so the subscriber's signature roots the plain event in the compiled closure
// and its broadcast plan is compiled; owned by PocoEventPublishTests alone.

/// <summary>No marker, no base list — the shape a clean domain layer wants.</summary>
public sealed record PocoOrderPlaced(int Id);

public sealed class PocoOrderPlacedHandler : IEventHandler<PocoOrderPlaced>
{
    public static int Received;

    public ValueTask HandleAsync(PocoOrderPlaced @event, ErgosfareContext context)
    {
        Received = @event.Id;
        return default;
    }
}

/// <summary>
/// A domain event declared with no Ergosfare reference at all. Requiring <see cref="IEvent"/>
/// on the message pushed that reference into the layer that declares domain events, which
/// runs the wrong way: the domain should not know which mediator publishes it.
/// </summary>
/// <remarks>
/// Nothing in the publish path ever needed the marker — <c>IEventHandler&lt;TEvent&gt;</c>,
/// <c>PublishAsync&lt;TEvent&gt;</c> and <c>FrozenBroadcastDispatch&lt;TEvent&gt;</c> are all
/// declared over <c>notnull</c>, because a broadcast carries no result. What needed it was
/// being seen: a plain type has no base list, so its declaration is never visited, and it had
/// no pipeline for a publish to travel. The subscriber's signature settles that now, and the
/// generator compiles the plan from it.
/// </remarks>
public class PocoEventPublishTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task APlainDomainEvent_ReachesItsSubscriber()
    {
        PocoOrderPlacedHandler.Received = 0;

        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<PocoOrderPlacedHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IEventMediator>();

        // It used to return normally here and deliver nothing: the handler was registered,
        // the message had no pipeline to reach it with, and PublishAsync defaults
        // throwIfNoHandlerFound to false, so the silence was complete.
        await mediator.PublishAsync(new PocoOrderPlaced(42));

        Assert.Equal(42, PocoOrderPlacedHandler.Received);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task APlainDomainEvent_CanBeRegisteredByHand()
    {
        PocoOrderPlacedHandler.Received = 0;

        // Registering the message names a type that implements no marker. The lane asks for
        // notnull, so this compiles — and selecting a message is inert either way, since the
        // catalog only ever asks whether a *participant* was selected.
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<PocoOrderPlaced>();
                e.Register<PocoOrderPlacedHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IEventMediator>();

        await mediator.PublishAsync(new PocoOrderPlaced(7));

        Assert.Equal(7, PocoOrderPlacedHandler.Received);
    }
}
