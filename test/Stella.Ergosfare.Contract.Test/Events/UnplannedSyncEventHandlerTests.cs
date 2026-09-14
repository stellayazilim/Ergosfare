using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Events;

// The synchronous subscriber shapes. The bare shape is top-level and unkeyed so the
// generator sees it and declines the publish. The ValueTask shape CANNOT be: suspicious
// behavior 10 has a broadcast flavor on the rewritten engine — an unkeyed
// IHandler<TEvent, ValueTask> subscriber is baked into the emitted broadcast plan as a
// HandleAsync call it does not have, and the generated file fails to compile with CS1061
// (verified on this branch). A compile error cannot be pinned by a test, so that pair
// stays excluded from discovery and reaches the same throw through hand registration.
//
// The handlers carry the IEvent marker themselves for the same reason the command-side
// shapes carry ICommand (suite README, suspicious behavior 9).

/// <summary>Event whose only subscriber is ValueTask-shaped and synchronous.</summary>
[ExcludeFromDiscovery]
public sealed class TaskShapedEvent : IEvent;

/// <inheritdoc cref="TaskShapedEvent"/>
[ExcludeFromDiscovery]
public sealed class TaskShapedEventHandler : IEvent, IHandler<TaskShapedEvent, ValueTask>
{
    /// <inheritdoc />
    public ValueTask Handle(TaskShapedEvent message, ErgosfareContext context)
    {
        context.Mark("sync:valuetask");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Event with a bare-shaped synchronous subscriber beside an asynchronous one.</summary>
public sealed class BareShapedEvent : IEvent;

/// <inheritdoc cref="BareShapedEvent"/>
public sealed class BareShapedEventHandler : IEvent, IHandler<BareShapedEvent, object>
{
    /// <inheritdoc />
    public object Handle(BareShapedEvent message, ErgosfareContext context)
    {
        context.Mark("sync:object");
        return "ignored";
    }
}

/// <summary>The asynchronous sibling the synchronous shape sits beside.</summary>
public sealed class BareShapedEventAsyncSibling : IEventHandler<BareShapedEvent>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(BareShapedEvent @event, ErgosfareContext context)
    {
        context.Mark("async");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The synchronous event-subscriber feature is dead with the runtime fan-out: a broadcast
/// plan cannot call the synchronous handler contracts, so an event with such a subscriber
/// has no compiled plan — and a publish that would reach somebody without a plan fails
/// loudly rather than silently skipping them. Only a publish reaching <em>nobody</em>
/// stays a silent no-op.
/// </summary>
public sealed class UnplannedSyncEventHandlerTests
{
    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddEventModule(events => events
                    .Register<TaskShapedEventHandler>()
                    .Register<BareShapedEventHandler>()
                    .Register<BareShapedEventAsyncSibling>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_publish_whose_subscriber_is_ValueTask_shaped_fails_unplanned()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IEventMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.PublishAsync(new TaskShapedEvent()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Equal(typeof(TaskShapedEvent), thrown.MessageType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_asynchronous_sibling_does_not_rescue_a_publish_with_a_synchronous_shape()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var mediator = provider.GetRequiredService<IEventMediator>();

        // The publish is unplanned as a whole: the asynchronous sibling never runs either,
        // because delivering to half the audience would be the silent-degradation the
        // rewrite removed.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.PublishAsync(new BareShapedEvent(), recorder.Events()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Empty(recorder.Stages);
    }
}
