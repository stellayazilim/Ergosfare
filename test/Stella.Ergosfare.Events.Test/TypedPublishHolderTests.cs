using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Covers the static-generic invoker holder behind the typed publish overload: it must be
/// the same instance the runtime-type cache serves (one plan cache per event type), and a
/// generic call made through a base-typed variable must keep dispatching by the event's
/// runtime type.
/// </summary>
public class TypedPublishHolderTests
{
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
    public void Holder_ServesTheSameInstance_AsTheRuntimeTypeCache()
    {
        var fromHolder = EventBroadcastInvokerCache.Holder<DerivedEvent>.Instance;
        var fromCache = EventBroadcastInvokerCache.Get(typeof(DerivedEvent));

        Assert.Same(fromCache, fromHolder);
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
