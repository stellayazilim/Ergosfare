using System.Reflection;
using Ergosfare.Core;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Events.Abstractions;
using Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Ergosfare.Events.Test;

public class AsyncBroadcastMediationStrategyTests
(ITestOutputHelper  testOutputHelper)
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldThrowWhenNoHandlerFound()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        messageRegistry.Register(typeof(StubNonGenericEvent));

        var messageMediator = new MessageMediator(
            
                messageRegistry,
                new EventHub(),
                new MessageDependenciesFactory(services));
        
        var mediator = new EventMediator(
            new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(messageRegistry),
            new ResultAdapterService(),
            messageMediator
            );

        Exception? exception = null;
        try
        {
           await mediator.PublishAsync(new StubNonGenericEvent(), new EventMediationSettings()
           {
               ThrowIfNoHandlerFound = true
           });

        }
        catch (Exception ex)
        {
            exception = ex;
        }
        testOutputHelper.WriteLine(messageRegistry.First().MessageType.FullName);
        Assert.NotNull(exception);
         
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldNotThrowWhenNoHandlerFound()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        messageRegistry.Register(typeof(StubNonGenericEvent));

        var messageMediator = new MessageMediator(
            messageRegistry,
            new EventHub(),
            new MessageDependenciesFactory(services));
        
        var mediator = new EventMediator(
            new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(messageRegistry),
            new ResultAdapterService(),
            messageMediator
        );

        Exception? exception = null;
        try
        {
            await mediator.PublishAsync(new StubNonGenericEvent(), new EventMediationSettings()
            {
                ThrowIfNoHandlerFound = false
            });

        }
        catch (Exception ex)
        {
            exception = ex;
        }
        Assert.Null(exception);
         
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRunHandlers()
    {
        var services = new ServiceCollection()
            .AddErgosfare(builder =>
            {
                builder.AddEventModule(x => { x.RegisterFromAssembly(Assembly.GetExecutingAssembly()); });
            })
            .BuildServiceProvider();

        var mediator = services.GetRequiredService<IPublisher>();
        var handler = services.GetRequiredService<StubNonGenericEventHandler1>();
        var result =  mediator.PublishAsync(new StubNonGenericEvent());

        Assert.NotNull(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldThrowWhileRunningHandlers()
    {
        var services = new ServiceCollection()
            .AddErgosfare(builder =>
            {
                builder.AddEventModule(x =>
                {
                    x.RegisterFromAssembly(Assembly.GetExecutingAssembly());
                });
            })
            .BuildServiceProvider();

        var mediator = services.GetRequiredService<IPublisher>();

   
        await mediator.PublishAsync(new StubNonGenericEventThrows());
        Assert.True(StubNonGenericEventHandlerThrows.IsRuned);
        Assert.True(StubNonGenericEventExceptionInterceptor.IsRuned);
    }
}