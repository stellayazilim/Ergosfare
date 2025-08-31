


using System.Reflection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Events.Abstractions;
using Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Events.Test;

public class EventMediatorTests
{
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
