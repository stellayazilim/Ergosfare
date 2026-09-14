using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator bakes the covariant
// subscribers into the compiled broadcast plan; every type is owned by
// IndirectHandlerBroadcastTests alone, and the container below registers the event's full
// compiled pipeline.

/// <summary>
/// The per-class supertype the indirect handlers are written against.
/// </summary>
public interface IIndirectTracedEvent : IEvent
{
    List<string> Trace { get; }
}

public sealed record IndirectOrderPlaced : IIndirectTracedEvent
{
    public List<string> Trace { get; } = [];
}

public sealed class IndirectOrderPlacedHandler : IEventHandler<IndirectOrderPlaced>
{
    public ValueTask HandleAsync(IndirectOrderPlaced message, ErgosfareContext context)
    {
        message.Trace.Add("direct");
        return default;
    }
}

public sealed class IndirectTracedEventHandler : IEventHandler<IIndirectTracedEvent>
{
    public ValueTask HandleAsync(IIndirectTracedEvent message, ErgosfareContext context)
    {
        message.Trace.Add("indirect");
        return default;
    }
}

[Group("ind.audit")]
public sealed class IndirectAuditTracedEventHandler : IEventHandler<IIndirectTracedEvent>
{
    public ValueTask HandleAsync(IIndirectTracedEvent message, ErgosfareContext context)
    {
        message.Trace.Add("audit");
        return default;
    }
}

/// <summary>
/// Event broadcast delivers to covariantly matched (base/interface-registered) handlers:
/// direct registrations first, then indirect ones — both ordinary planned participants of
/// the compiled broadcast plan. Opting out of broad delivery is a group concern — an
/// indirect handler in a non-default group only runs when a publish selects its group.
/// </summary>
public class IndirectHandlerBroadcastTests
{
    private static IEventMediator BuildMediator()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e
                .Register<IndirectOrderPlaced>()
                .Register(typeof(IndirectOrderPlacedHandler))
                .Register(typeof(IndirectTracedEventHandler))
                .Register(typeof(IndirectAuditTracedEventHandler))))
            .BuildServiceProvider()
            .GetRequiredService<IEventMediator>();

    [Fact]
    public async Task Broadcast_DeliversToIndirectHandlers_DirectFirst()
    {
        var mediator = BuildMediator();
        var @event = new IndirectOrderPlaced();

        await mediator.PublishAsync(@event, CancellationToken.None);

        // The audit-group handler stays out of default delivery.
        Assert.Equal(["direct", "indirect"], @event.Trace);
    }

    [Fact]
    public async Task GroupFilteredPublish_SelectsOnlyMatchingIndirectHandlers()
    {
        var mediator = BuildMediator();
        var @event = new IndirectOrderPlaced();

        await mediator.PublishAsync(@event, ["ind.audit"], CancellationToken.None);

        Assert.Equal(["audit"], @event.Trace);
    }
}
