using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Contains unit tests for <see cref="EventModule"/> registration and behavior,
/// ensuring that event handlers are correctly registered and non-event handlers are rejected.
/// </summary>
public class EventModuleTests
{
    /// <summary>
    /// A non-event handler stub used to test invalid registrations.
    /// </summary>
    private class NonEventHandler: IHandler<IMessage, Task>
    {
        /// <summary>
        /// Handles a message but performs no operation.
        /// </summary>
        public Task Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Tests that the <see cref="EventModule"/> registers handlers correctly,
    /// including handlers from the current assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldRegisterEventModule()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(c =>
                c.Register<StubNonGenericEventHandler1>()
                    .RegisterFromAssembly(Assembly.GetExecutingAssembly())
            )).BuildServiceProvider();
        var mediator = serviceCollection.GetRequiredService<IEventMediator>();

        var result = mediator.PublishAsync((IEvent) new StubNonGenericEvent());
        var genericResult = mediator.PublishAsync<StubNonGenericEvent>(new StubNonGenericEvent());
        Assert.NotNull(result);
        Assert.NotNull(genericResult);
        
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