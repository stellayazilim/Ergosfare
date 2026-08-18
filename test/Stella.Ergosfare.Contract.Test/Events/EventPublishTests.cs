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

// Top-level and unkeyed so the generator bakes the broadcast plans; every event stays
// scoped to this area and no interceptor targets a marker type.

/// <summary>Event two handlers subscribe to.</summary>
public sealed class OrderPlaced : IEvent;

/// <inheritdoc />
public sealed class OrderPlacedInvoiceHandler : IEventHandler<OrderPlaced>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(OrderPlaced @event, ErgosfareContext context)
    {
        context.Mark("invoice");
        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc />
public sealed class OrderPlacedWarehouseHandler : IEventHandler<OrderPlaced>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(OrderPlaced @event, ErgosfareContext context)
    {
        context.Mark("warehouse");
        return ValueTask.CompletedTask;
    }
}

/// <summary>The generator sees this event, but nothing handles it anywhere.</summary>
public sealed class NobodyListens : IEvent;

/// <summary>Never discovered at all — a different situation from <see cref="NobodyListens"/>.</summary>
[ExcludeFromDiscovery]
public sealed class UnknownEvent : IEvent;

/// <summary>Event with an ungrouped and a grouped handler.</summary>
public sealed class StockChanged : IEvent;

/// <inheritdoc />
public sealed class StockChangedDefaultHandler : IEventHandler<StockChanged>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(StockChanged @event, ErgosfareContext context)
    {
        context.Mark("default");
        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc />
[Group(EventPublishTests.Reporting)]
public sealed class StockChangedReportingHandler : IEventHandler<StockChanged>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(StockChanged @event, ErgosfareContext context)
    {
        context.Mark("reporting");
        return ValueTask.CompletedTask;
    }
}

/// <summary>The exception a broadcast handler throws, distinguishable from framework ones.</summary>
public sealed class BroadcastFailure() : Exception("broadcast handler failed");

/// <summary>An event whose interceptor stages record the result slot they are handed.</summary>
public sealed class Announced : IEvent
{
    /// <summary>Drives the handler into the failure path, so the exception stage is reached.</summary>
    public bool Fail;
}

/// <inheritdoc />
public sealed class AnnouncedHandler : IEventHandler<Announced>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Announced @event, ErgosfareContext context)
    {
        context.Mark("handler");

        if (@event.Fail)
        {
            throw new BroadcastFailure();
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The core result-agnostic contracts, not the event facades: the facades hide the slot
/// behind their own default implementations, and the slot is what these scenarios pin.
/// The <see cref="IEvent"/> marker is what makes the module builder accept them.
/// </summary>
public sealed class AnnouncedPost : IEvent, IAsyncPostInterceptor<Announced>
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(Announced @event, object result, ErgosfareContext context)
    {
        context.Mark("post", EventPublishTests.Describe(result));
        return ValueTask.FromResult(result);
    }
}

/// <inheritdoc cref="AnnouncedPost"/>
public sealed class AnnouncedException : IEvent, IAsyncExceptionInterceptor<Announced>
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(
        Announced @event, object? result, Exception exception, ErgosfareContext context)
    {
        context.Mark("exception", EventPublishTests.Describe(result));
        return ValueTask.FromResult(result!);
    }
}

/// <inheritdoc cref="AnnouncedPost"/>
public sealed class AnnouncedFinal : IEvent, IAsyncFinalInterceptor<Announced>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(
        Announced @event, object? result, Exception? exception, ErgosfareContext context)
    {
        context.Mark("final", EventPublishTests.Describe(result));
        return ValueTask.CompletedTask;
    }
}

/// <summary>An event whose post stage aborts after the handlers have run.</summary>
public sealed class Recalled : IEvent;

/// <inheritdoc />
public sealed class RecalledHandler : IEventHandler<Recalled>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Recalled @event, ErgosfareContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc cref="AnnouncedPost"/>
public sealed class RecalledAbortingPost : IEvent, IAsyncPostInterceptor<Recalled>
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(Recalled @event, object result, ErgosfareContext context)
    {
        context.Mark("post:abort");
        context.Abort();
        return ValueTask.FromResult(result);
    }
}

/// <inheritdoc cref="AnnouncedPost"/>
public sealed class RecalledFinal : IEvent, IAsyncFinalInterceptor<Recalled>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(
        Recalled @event, object? result, Exception? exception, ErgosfareContext context)
    {
        context.Mark("final",
            $"{EventPublishTests.Describe(result)}|{(exception is null ? "none" : exception.GetType().Name)}");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Publish semantics: fan-out to every matching handler, what an audience of nobody does,
/// and group filtering behaving exactly as it does for commands.
/// </summary>
public sealed class EventPublishTests
{
    /// <summary>The group name this area filters on.</summary>
    public const string Reporting = "ev-reporting";

    /// <inheritdoc cref="Pipeline.PipelineVocabulary.Describe(object?)"/>
    public static string Describe(object? result) => result switch
    {
        null => "null",
        Unit unit => ReferenceEquals(unit, Unit.Value) ? nameof(Unit) : "unit:other",
        _ => result.ToString() ?? result.GetType().Name,
    };

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddEventModule(events => events
                    .Register<OrderPlacedInvoiceHandler>()
                    .Register<OrderPlacedWarehouseHandler>()
                    .Register<StockChangedDefaultHandler>()
                    .Register<StockChangedReportingHandler>()
                    .Register<AnnouncedHandler>()
                    .Register<AnnouncedPost>()
                    .Register<AnnouncedException>()
                    .Register<AnnouncedFinal>()
                    .Register<RecalledHandler>()
                    .Register<RecalledAbortingPost>()
                    .Register<RecalledFinal>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publish_reaches_every_registered_handler()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>().PublishAsync(new OrderPlaced(), recorder.Events());

        recorder.AssertStages("invoice", "warehouse");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publishing_an_event_nobody_handles_is_a_no_op()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        // A publish reaching nobody is not an error at run time — with or without a plan,
        // because there is nothing a plan could have run.
        await provider.GetRequiredService<IEventMediator>().PublishAsync(new NobodyListens(), recorder.Events());

        recorder.AssertStages();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publishing_an_unregistered_event_type_is_a_no_op_like_a_registered_one()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IEventMediator>();

        Assert.Null(await Record.ExceptionAsync(
            async () => await mediator.PublishAsync(new UnknownEvent())));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_default_publish_skips_grouped_handlers()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>().PublishAsync(new StockChanged(), recorder.Events());

        recorder.AssertStages("default");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_grouped_publish_reaches_only_the_grouped_handlers()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        await provider.GetRequiredService<IEventMediator>().PublishAsync(
            new StockChanged(), recorder.Events(), [Reporting]);

        recorder.AssertStages("reporting");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_publishs_post_and_final_stages_are_handed_the_shared_unit_instance()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>().PublishAsync(new Announced(), recorder.Events());

        // A publish never produces a result, so unlike a void command its slot is filled
        // before the fan-out and stays filled — there is no "not yet" on this path.
        recorder.AssertStages("handler", "post", "final");
        Assert.Equal(nameof(Unit), recorder.DetailOf("post"));
        Assert.Equal(nameof(Unit), recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_failed_publishs_exception_and_final_stages_see_the_empty_result_slot()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>()
            .PublishAsync(new Announced { Fail = true }, recorder.Events());

        // Changed by the plan lane, pinned as observed: the compiled broadcast plan fills
        // the slot only after each handler completes, so a handler that throws leaves the
        // exception and final stages the empty slot — null — where the retired runtime
        // fan-out handed them Unit.Value up front. A publish now behaves like a void
        // command: the slot is empty until the handler has run. See the suite README's
        // suspicious-behaviors entry on the runtime-lane removal.
        recorder.AssertStages("handler", "exception", "final");
        Assert.Equal("null", recorder.DetailOf("exception"));
        Assert.Equal("null", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_a_publish_reaches_the_publisher()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        // A publish is stopped like any other dispatch: the exception stage never sees the
        // abort, the final stage does not run either, and the publisher is told.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await provider.GetRequiredService<IEventMediator>()
                .PublishAsync(new Recalled(), recorder.Events()));

        recorder.AssertStages("handler", "post:abort");
    }
}
