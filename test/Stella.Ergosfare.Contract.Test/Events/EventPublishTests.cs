using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
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
    public async Task Publishing_an_unregistered_event_type_throws_even_without_asking()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IEventMediator>();

        // Unlike NobodyListens, this type is not in the registry at all — see the README.
        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.PublishAsync(new UnknownEvent()));
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
}
