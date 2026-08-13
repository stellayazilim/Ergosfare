using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Covers the static-generic slot behind the typed publish overload: it must serve the same
/// pipeline the runtime-type lookup does (one composition cache per message type), it must not
/// leak between containers, and a generic call made through a base-typed variable must keep
/// dispatching by the event's runtime type.
/// </summary>
public class TypedPublishHolderTests
{
    private sealed class NoCompositionFactory : IMessageDependenciesFactory
    {
        public IMessageDependencies Create(Type messageType, IEnumerable<string> groups)
            => throw new NotSupportedException();

        public IMessageDependencies? Find(Type messageType, IEnumerable<string> groups) => null;
    }

    public class BaseEvent : IEvent { }

    public sealed class DerivedEvent : BaseEvent { }

    public sealed class DerivedEventHandler : IEventHandler<DerivedEvent>
    {
        public ValueTask HandleAsync(DerivedEvent @event, ErgosfareContext context)
        {
            context.Set("derivedRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TypedSlot_ServesTheSameInstance_AsTheRuntimeTypeLookup()
    {
        var table = new BroadcastDispatchTable(new NoCompositionFactory());

        var fromSlot = table.Get<DerivedEvent>();
        var fromLookup = table.Get(typeof(DerivedEvent));

        // Two entries per type would mean two composition caches and two gate verdicts for
        // one pipeline — the typed slot has to be a shortcut to the dictionary, not a second
        // store beside it.
        Assert.Same(fromLookup, fromSlot);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TypedSlot_DoesNotLeakBetweenContainers()
    {
        var first = new BroadcastDispatchTable(new NoCompositionFactory());
        var second = new BroadcastDispatchTable(new NoCompositionFactory());

        var fromFirst = first.Get<DerivedEvent>();
        var fromSecond = second.Get<DerivedEvent>();

        // The slot is process-wide while the table is per container, so it carries a table
        // identity check; without it the second container would be served the first's
        // pipeline — and with it, the first still reads its own on the next publish.
        Assert.NotSame(fromFirst, fromSecond);
        Assert.Same(fromFirst, first.Get<DerivedEvent>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ThroughABaseTypedVariable_DispatchesByRuntimeType()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<DerivedEventHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        // The variable's static type closes the generic overload over BaseEvent; the
        // holder guard must reject it and resolve the DerivedEvent pipeline instead.
        BaseEvent @event = new DerivedEvent();
        var settings = new EventMediationSettings();

        await mediator.PublishAsync(@event, settings);

        Assert.Equal(true, settings.Items["derivedRan"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ThroughTheConcreteType_TakesTheHolderAndDispatches()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<DerivedEventHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        var settings = new EventMediationSettings();

        await mediator.PublishAsync(new DerivedEvent(), settings);

        Assert.Equal(true, settings.Items["derivedRan"]);
    }
}
