using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Event broadcast delivers to covariantly matched (base/interface-registered) handlers:
/// direct registrations first, then indirect ones. Opting out of broad delivery is a group
/// concern — an indirect handler in a non-default group only runs when a publish selects
/// its group.
/// </summary>
/// <remarks>
/// The message registry is process-wide, so the stubs are inert everywhere else:
/// parameterless constructors, recording into the published event instance, and
/// <c>[ExcludeFromDiscovery]</c> so other tests' assembly scans never register them.
/// </remarks>
public class IndirectHandlerBroadcastTests
{
    [ExcludeFromDiscovery]
    public interface ITracedEvent : IEvent
    {
        List<string> Trace { get; }
    }

    [ExcludeFromDiscovery]
    public sealed record OrderPlaced : ITracedEvent
    {
        public List<string> Trace { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class OrderPlacedHandler : IEventHandler<OrderPlaced>
    {
        public ValueTask HandleAsync(OrderPlaced message, IExecutionContext context)
        {
            message.Trace.Add("direct");
            return default;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class TracedEventHandler : IEventHandler<ITracedEvent>
    {
        public ValueTask HandleAsync(ITracedEvent message, IExecutionContext context)
        {
            message.Trace.Add("indirect");
            return default;
        }
    }

    [ExcludeFromDiscovery]
    [Group("audit")]
    public sealed class AuditTracedEventHandler : IEventHandler<ITracedEvent>
    {
        public ValueTask HandleAsync(ITracedEvent message, IExecutionContext context)
        {
            message.Trace.Add("audit");
            return default;
        }
    }

    private static IEventMediator BuildMediator()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e
                .Register<OrderPlaced>()
                .Register(typeof(OrderPlacedHandler))
                .Register(typeof(TracedEventHandler))
                .Register(typeof(AuditTracedEventHandler))))
            .BuildServiceProvider()
            .GetRequiredService<IEventMediator>();

    [Fact]
    public async Task Broadcast_DeliversToIndirectHandlers_DirectFirst()
    {
        var mediator = BuildMediator();
        var @event = new OrderPlaced();

        await mediator.PublishAsync(@event, CancellationToken.None);

        // The audit-group handler stays out of default delivery.
        Assert.Equal(["direct", "indirect"], @event.Trace);
    }

    [Fact]
    public async Task GroupFilteredPublish_SelectsOnlyMatchingIndirectHandlers()
    {
        var mediator = BuildMediator();
        var @event = new OrderPlaced();

        await mediator.PublishAsync(@event, ["audit"], CancellationToken.None);

        Assert.Equal(["audit"], @event.Trace);
    }
}
