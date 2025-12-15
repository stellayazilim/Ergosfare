using System.Reflection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Contains unit tests for <see cref="IEventMediator"/>, 
/// ensuring that events are published correctly through the mediator.
/// </summary>
public class EventMediatorTests
{
    /// <summary>
    /// Tests that <see cref="IEventMediator.PublishAsync"/> correctly publishes a <see cref="StubNonGenericEvent"/>
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
                    e => e.RegisterFromAssembly(Assembly.GetExecutingAssembly())
                    ))
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IEventMediator>();
        await mediator.PublishAsync(new StubNonGenericEvent(), CancellationToken.None);
        await mediator.PublishAsync(new StubNonGenericEvent(), ["*"],  CancellationToken.None);
    }
}
