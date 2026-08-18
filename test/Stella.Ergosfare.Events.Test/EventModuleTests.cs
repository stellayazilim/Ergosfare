using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// A stub event owned by <see cref="EventModuleTests"/>; top-level so the source generator
/// compiles its broadcast plan.
/// </summary>
public record ModuleStubEvent : IEvent;

/// <summary>
/// The one compiled subscriber of <see cref="ModuleStubEvent"/>.
/// </summary>
public sealed class ModuleStubEventHandler : IEventHandler<ModuleStubEvent>
{
    public ValueTask HandleAsync(ModuleStubEvent message, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>
/// Contains unit tests for <see cref="EventModule"/> registration and behavior,
/// ensuring that event handlers are correctly registered and non-event handlers are rejected.
/// </summary>
public class EventModuleTests
{
    /// <summary>
    /// A non-event handler stub used to test invalid registrations.
    /// </summary>
    private class NonEventHandler: IHandler<IMessage, ValueTask>
    {
        /// <summary>
        /// Handles a message but performs no operation.
        /// </summary>
        public ValueTask Handle(IMessage message, ErgosfareContext context)
        {
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Tests that the <see cref="EventModule"/> registers handlers correctly,
    /// including handlers from the current assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRegisterEventModule()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(c =>
                c.Register<ModuleStubEventHandler>()
            )).BuildServiceProvider();
        var mediator = serviceCollection.GetRequiredService<IEventMediator>();

        await mediator.PublishAsync((IEvent) new ModuleStubEvent());
        await mediator.PublishAsync(new ModuleStubEvent());

    }

    /// <summary>
    /// Tests that attempting to register a non-event handler to the <see cref="EventModule"/>
    /// throws a <see cref="NotSupportedException"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldNotRegisterNonEventsToEventModule()
    {
        var serviceCollection = new ServiceCollection();
        Assert.Throws<NotSupportedException>(() =>
        {
            serviceCollection.AddErgosfare(x => 
                x.AddEventModule(c =>
                    c.Register(typeof(NonEventHandler)))).BuildServiceProvider();
        });
    }

}