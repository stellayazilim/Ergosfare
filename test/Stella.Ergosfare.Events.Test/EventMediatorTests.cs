
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// A stub event owned by <see cref="EventMediatorTests"/>; top-level so the source
/// generator compiles its broadcast plan.
/// </summary>
public record MediatorStubEvent : IEvent;

/// <summary>
/// The event's default-group subscriber.
/// </summary>
public sealed class MediatorStubEventHandler : IEventHandler<MediatorStubEvent>
{
    public ValueTask HandleAsync(MediatorStubEvent message, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>
/// The event's custom-group subscriber; the grouped publish below names its set as a
/// literal, so the generator compiles a per-set plan for it.
/// </summary>
[Group("mediator.custom")]
public sealed class MediatorStubGroupedHandler : IEventHandler<MediatorStubEvent>
{
    public ValueTask HandleAsync(MediatorStubEvent message, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>
/// Contains unit tests for <see cref="IEventMediator"/>,
/// ensuring that events are published correctly through the mediator.
/// </summary>
public class EventMediatorTests
{
    /// <summary>
    /// Tests that <see cref="IEventMediator.PublishAsync(IEvent, IEnumerable{string}, CancellationToken)"/> correctly publishes a <see cref="MediatorStubEvent"/>
    /// both with default and custom group settings.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Event", "Coverage")]
    public async Task ShouldPublishEvent()
    {
        var services = new ServiceCollection()
            .AddErgosfare(
                x => x.AddEventModule(
                    e =>
                    {
                        e.Register<MediatorStubEventHandler>();
                        e.Register<MediatorStubGroupedHandler>();
                    }))
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IEventMediator>();
        await mediator.PublishAsync(new MediatorStubEvent(), CancellationToken.None);
        await mediator.PublishAsync(new MediatorStubEvent(), ["mediator.custom"], CancellationToken.None);
    }
}
