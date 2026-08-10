using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Events;

/// <summary>
/// Publish semantics: fan-out to every matching handler, what an audience of nobody does,
/// and group filtering behaving exactly as it does for commands.
/// </summary>
public sealed class EventPublishTests
{
    private const string Key = "contract.events";
    private const string Reporting = "reporting";

    [DiscoveryKey(Key)]
    public sealed class OrderPlaced : IEvent;

    [DiscoveryKey(Key)]
    public sealed class OrderPlacedInvoiceHandler : IEventHandler<OrderPlaced>
    {
        public ValueTask HandleAsync(OrderPlaced @event, IExecutionContext context)
        {
            context.Mark("invoice");
            return ValueTask.CompletedTask;
        }
    }

    [DiscoveryKey(Key)]
    public sealed class OrderPlacedWarehouseHandler : IEventHandler<OrderPlaced>
    {
        public ValueTask HandleAsync(OrderPlaced @event, IExecutionContext context)
        {
            context.Mark("warehouse");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Registered as a message, but nothing handles it.</summary>
    [DiscoveryKey(Key)]
    public sealed class NobodyListens : IEvent;

    /// <summary>Never registered at all — a different situation from <see cref="NobodyListens"/>.</summary>
    [ExcludeFromDiscovery]
    public sealed class UnknownEvent : IEvent;

    [DiscoveryKey(Key)]
    public sealed class StockChanged : IEvent;

    [DiscoveryKey(Key)]
    public sealed class StockChangedDefaultHandler : IEventHandler<StockChanged>
    {
        public ValueTask HandleAsync(StockChanged @event, IExecutionContext context)
        {
            context.Mark("default");
            return ValueTask.CompletedTask;
        }
    }

    [DiscoveryKey(Key)]
    [Group(Reporting)]
    public sealed class StockChangedReportingHandler : IEventHandler<StockChanged>
    {
        public ValueTask HandleAsync(StockChanged @event, IExecutionContext context)
        {
            context.Mark("reporting");
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options.AddEventModule(events => events.RegisterGenerated(Key)))
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
    public async Task Publishing_a_registered_event_nobody_handles_is_a_no_op()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>().PublishAsync(new NobodyListens(), recorder.Events());

        recorder.AssertStages();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publishing_a_registered_event_nobody_handles_throws_when_the_caller_asks_it_to()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new EventMediationSettings { ThrowIfNoHandlerFound = true };

        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.PublishAsync(new NobodyListens(), settings));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publishing_an_unregistered_event_type_is_a_no_op_like_a_registered_one()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        // UnknownEvent is not in the registry at all, NobodyListens is registered with no
        // handlers. Both reach nobody, and both are silent unless the caller asks — the
        // flag used to govern only the second.
        await provider.GetRequiredService<IEventMediator>().PublishAsync(new UnknownEvent(), recorder.Events());

        recorder.AssertStages();
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
        var settings = recorder.Events();
        settings.Filters.Groups = [Reporting];

        await provider.GetRequiredService<IEventMediator>().PublishAsync(new StockChanged(), settings);

        recorder.AssertStages("reporting");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publishing_an_unregistered_event_type_throws_when_the_caller_asks_it_to()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new EventMediationSettings { ThrowIfNoHandlerFound = true };

        // The other half of the flag's new reach: what the unregistered case used to do
        // unconditionally, it now does on request — the same as the registered-but-unhandled
        // case two scenarios up. Appended rather than placed beside its sibling: a member
        // inserted above renumbers the state machines below it and churns the lane map.
        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.PublishAsync(new UnknownEvent(), settings));
    }

    // -----------------------------------------------------------------------
    // what a publish puts in its result slot
    //
    // Appended for the same reason as the scenario above.
    // -----------------------------------------------------------------------

    /// <summary>The exception a broadcast handler throws, distinguishable from framework ones.</summary>
    public sealed class BroadcastFailure() : Exception("broadcast handler failed");

    /// <summary>An event whose interceptor stages record the result slot they are handed.</summary>
    [DiscoveryKey(Key)]
    public sealed class Announced : IEvent
    {
        /// <summary>Drives the handler into the failure path, so the exception stage is reached.</summary>
        public bool Fail;
    }

    [DiscoveryKey(Key)]
    public sealed class AnnouncedHandler : IEventHandler<Announced>
    {
        public ValueTask HandleAsync(Announced @event, IExecutionContext context)
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
    [DiscoveryKey(Key)]
    public sealed class AnnouncedPost : IEvent, IAsyncPostInterceptor<Announced>
    {
        public ValueTask<object> HandleAsync(Announced @event, object result, IExecutionContext context)
        {
            context.Mark("post", Describe(result));
            return ValueTask.FromResult(result);
        }
    }

    /// <inheritdoc cref="AnnouncedPost"/>
    [DiscoveryKey(Key)]
    public sealed class AnnouncedException : IEvent, IAsyncExceptionInterceptor<Announced>
    {
        public ValueTask<object> HandleAsync(
            Announced @event, object? result, Exception exception, IExecutionContext context)
        {
            context.Mark("exception", Describe(result));
            return ValueTask.FromResult(result!);
        }
    }

    /// <inheritdoc cref="AnnouncedPost"/>
    [DiscoveryKey(Key)]
    public sealed class AnnouncedFinal : IEvent, IAsyncFinalInterceptor<Announced>
    {
        public ValueTask HandleAsync(
            Announced @event, object? result, Exception? exception, IExecutionContext context)
        {
            context.Mark("final", Describe(result));
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="Pipeline.PipelineVocabulary.Describe(object?)"/>
    private static string Describe(object? result) => result switch
    {
        null => "null",
        Unit unit => ReferenceEquals(unit, Unit.Value) ? nameof(Unit) : "unit:other",
        _ => result.ToString() ?? result.GetType().Name,
    };

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
    public async Task A_failed_publishs_exception_and_final_stages_are_handed_the_shared_unit_instance()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>()
            .PublishAsync(new Announced { Fail = true }, recorder.Events());

        recorder.AssertStages("handler", "exception", "final");
        Assert.Equal(nameof(Unit), recorder.DetailOf("exception"));
        Assert.Equal(nameof(Unit), recorder.DetailOf("final"));
    }

    // -----------------------------------------------------------------------
    // aborting a publish
    // -----------------------------------------------------------------------

    /// <summary>An event whose post stage aborts after the handlers have run.</summary>
    [DiscoveryKey(Key)]
    public sealed class Recalled : IEvent;

    [DiscoveryKey(Key)]
    public sealed class RecalledHandler : IEventHandler<Recalled>
    {
        public ValueTask HandleAsync(Recalled @event, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="AnnouncedPost"/>
    [DiscoveryKey(Key)]
    public sealed class RecalledAbortingPost : IEvent, IAsyncPostInterceptor<Recalled>
    {
        public ValueTask<object> HandleAsync(Recalled @event, object result, IExecutionContext context)
        {
            context.Mark("post:abort");
            context.Abort();
            return ValueTask.FromResult(result);
        }
    }

    /// <inheritdoc cref="AnnouncedPost"/>
    [DiscoveryKey(Key)]
    public sealed class RecalledFinal : IEvent, IAsyncFinalInterceptor<Recalled>
    {
        public ValueTask HandleAsync(
            Recalled @event, object? result, Exception? exception, IExecutionContext context)
        {
            context.Mark("final", $"{Describe(result)}|{(exception is null ? "none" : exception.GetType().Name)}");
            return ValueTask.CompletedTask;
        }
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
