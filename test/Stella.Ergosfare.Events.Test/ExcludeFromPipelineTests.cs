using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// End-to-end <c>[ExcludeFromPipeline]</c> behavior: an event-wide (covariant)
/// pre-interceptor is skipped for excluded events while interceptors registered against
/// the event type itself still run.
/// </summary>
/// <remarks>
/// The message registry is process-wide, so these stubs are built to be inert everywhere
/// else: parameterless constructors, recording into the published event instance itself
/// (other tests' events don't implement <see cref="ITracedEvent"/>), and
/// <c>[ExcludeFromDiscovery]</c> so assembly scans in other tests never register them —
/// this suite registers them explicitly.
/// </remarks>
public class ExcludeFromPipelineTests
{
    [ExcludeFromDiscovery]
    public interface ITracedEvent : IEvent
    {
        List<string> Trace { get; }
    }

    [ExcludeFromDiscovery]
    public sealed record ChattyEvent : ITracedEvent
    {
        public List<string> Trace { get; } = [];
    }

    [ExcludeFromDiscovery]
    [ExcludeFromPipeline]
    public sealed record QuietEvent : ITracedEvent
    {
        public List<string> Trace { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class BroadPre : IEventPreInterceptor
    {
        public ValueTask HandleAsync(IEvent @event, IExecutionContext context)
        {
            if (@event is ITracedEvent traced)
            {
                traced.Trace.Add("broad");
            }

            return default;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class QuietExactPre : IEventPreInterceptor<QuietEvent>
    {
        public ValueTask<object> HandleAsync(QuietEvent @event, IExecutionContext context)
        {
            @event.Trace.Add("exact");
            return new(@event);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class ChattyHandler : IEventHandler<ChattyEvent>
    {
        public ValueTask HandleAsync(ChattyEvent message, IExecutionContext context)
        {
            message.Trace.Add("handled");
            return default;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class QuietHandler : IEventHandler<QuietEvent>
    {
        public ValueTask HandleAsync(QuietEvent message, IExecutionContext context)
        {
            message.Trace.Add("handled");
            return default;
        }
    }

    private static IEventMediator BuildMediator()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e
                .Register<ChattyEvent>()
                .Register<QuietEvent>()
                .Register(typeof(BroadPre))
                .Register(typeof(QuietExactPre))
                .Register(typeof(ChattyHandler))
                .Register(typeof(QuietHandler))))
            .BuildServiceProvider()
            .GetRequiredService<IEventMediator>();

    [Fact]
    public async Task CovariantPreInterceptor_RunsForNormalEvents()
    {
        var mediator = BuildMediator();
        var @event = new ChattyEvent();

        await mediator.PublishAsync(@event, CancellationToken.None);

        Assert.Equal(["broad", "handled"], @event.Trace);
    }

    [Fact]
    public async Task ExcludedEvent_SkipsCovariantPreInterceptor_KeepsExactOne()
    {
        var mediator = BuildMediator();
        var @event = new QuietEvent();

        await mediator.PublishAsync(@event, CancellationToken.None);

        Assert.Equal(["exact", "handled"], @event.Trace);
    }
}
