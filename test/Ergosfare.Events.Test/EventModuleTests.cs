using System.Reflection;
using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Events.Abstractions;
using Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Events.Test;

public class EventModuleTests
{
    
    
    private class NonEventHandler: IHandler<IMessage, Task>
    {
        public Task Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

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