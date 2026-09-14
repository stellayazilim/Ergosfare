using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator compiles their broadcast
// plans; every type is owned by ExcludeFromPipelineTests alone. The covariant interceptor
// is declared against this class's own supertype rather than IEvent — a module-marker-wide
// interceptor would enter every event's compiled composition in the assembly.

/// <summary>
/// The per-class supertype the covariant pre-interceptor is written against.
/// </summary>
public interface IExcludePipelineTracedEvent : IEvent
{
    List<string> Trace { get; }
}

public sealed record ChattyEvent : IExcludePipelineTracedEvent
{
    public List<string> Trace { get; } = [];
}

[ExcludeFromPipeline]
public sealed record QuietEvent : IExcludePipelineTracedEvent
{
    public List<string> Trace { get; } = [];
}

/// <summary>
/// The covariant pre-interceptor: registered against the supertype, it enters the compiled
/// pipeline of every event assignable to it — except those excluded from the pipeline.
/// </summary>
public sealed class ExcludePipelineBroadPre : IEventPreInterceptor<IExcludePipelineTracedEvent>
{
    public ValueTask<IExcludePipelineTracedEvent> HandleAsync(IExcludePipelineTracedEvent @event, ErgosfareContext context)
    {
        @event.Trace.Add("broad");
        return new(@event);
    }
}

public sealed class QuietExactPre : IEventPreInterceptor<QuietEvent>
{
    public ValueTask<QuietEvent> HandleAsync(QuietEvent @event, ErgosfareContext context)
    {
        @event.Trace.Add("exact");
        return new(@event);
    }
}

public sealed class ChattyHandler : IEventHandler<ChattyEvent>
{
    public ValueTask HandleAsync(ChattyEvent message, ErgosfareContext context)
    {
        message.Trace.Add("handled");
        return default;
    }
}

public sealed class QuietHandler : IEventHandler<QuietEvent>
{
    public ValueTask HandleAsync(QuietEvent message, ErgosfareContext context)
    {
        message.Trace.Add("handled");
        return default;
    }
}

/// <summary>
/// <c>[ExcludeFromPipeline]</c> under the plan-only dispatch: a covariant
/// (supertype-registered) pre-interceptor is baked into the compiled plan of normal
/// events — and an excluded event has no plan at all, so publishing it fails loudly.
/// The plan builder skips excluded messages outright instead of modeling the exclusion
/// the way the frozen compositions do; until it learns to, the attribute makes an event
/// unpublishable rather than merely uncovarianted, and this class pins that.
/// </summary>
public class ExcludeFromPipelineTests
{
    private static IEventMediator BuildMediator()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e
                .Register<ChattyEvent>()
                .Register<QuietEvent>()
                .Register(typeof(ExcludePipelineBroadPre))
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
    public async Task ExcludedEvent_HasNoPlan_AndFailsThePublishLoudly()
    {
        var mediator = BuildMediator();
        var @event = new QuietEvent();

        // The old contract — the covariant interceptor stays out while the exact one and
        // the handler run — needs a plan that models the exclusion, and none is emitted.
        // Nothing is dispatched at run time that was not produced at compile time, so the
        // publish fails naming the gap instead of running a degraded pipeline.
        var failure = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.PublishAsync(@event, CancellationToken.None));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, failure.Reason);
        Assert.Empty(@event.Trace);
    }
}
