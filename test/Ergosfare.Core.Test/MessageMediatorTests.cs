using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Test.__stubs__;
using Moq;

namespace Ergosfare.Core.Test;

public class MessageMediatorTests
{
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageMediatorShouldThrowArgumentNullException()
    {       
        // arrange 
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        // act & assert
        Assert.Throws<ArgumentNullException>(
            () =>  new MessageMediator(null, null));
        Assert.Throws<ArgumentNullException>(
            () => new MessageMediator(registry, null));
        
    }


    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageMediatrShouldConstructedTest()
    {
        // arrange 
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        var messageDependencyFactory = new MessageDependenciesFactory(null);
        
        // act
        var mediator = new MessageMediator(registry, messageDependencyFactory);
        
        // assert
        Assert.NotNull(mediator);
    }
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task MessageMediatr_Mediate_ShouldThrowArgumentNullException()
    {
        // arrange 
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        var messageDependencyFactory = new MessageDependenciesFactory(null);
        var mediator = new MessageMediator(registry, messageDependencyFactory);

        // act && assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            mediator
                .Mediate<StubNonGenericMessage, Task>(
                    new StubNonGenericMessage(), null));
   
    }
    
    
    
        
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Blackbox")]
    public async Task MessageMediatr_Mediate_ShouldSetExecutionContext()
    {
        
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        registry.Register(typeof(StubNonGenericHandler));
        registry.Register(typeof(StubNonGenericPreInterceptor));
        registry.Register(typeof(StubNonGenericPreInterceptor2));
        registry.Register(typeof(StubNonGenericDerivedPreInterceptor));
        registry.Register(typeof(StubNonGenericDerivedPreInterceptor2));
        registry.Register(typeof(StubNonGenericPostInterceptor));
        registry.Register(typeof(StubNonGenericPostInterceptor2));
        registry.Register(typeof(StubNonGenericDerivedPostInterceptor));
        registry.Register(typeof(StubNonGenericDerivedPostInterceptor2));
        
        
        var mediator = new MessageMediator(
            registry,
            new MessageDependenciesFactory(null!)
        );

        var message = new StubNonGenericMessage();
        var options = new MediateOptions<StubNonGenericMessage, Task>
        {
            CancellationToken = CancellationToken.None,
            Items = new Dictionary<object, object?>(),
            MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry)!,
            MessageMediationStrategy = 
                new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>(),
            Groups = []
        };

        // Act
        #pragma warning disable CS4014
        mediator.Mediate(message, options);
        #pragma warning restore CS4014 

        
        // assert, 
        // since we dont awaited mediate call, capture when context set and assert
        while (AmbientExecutionContext.GetCurrentOrDefault() != null)
        {
            var currentContext = AmbientExecutionContext.GetCurrentOrDefault();
            Assert.Equal(options.CancellationToken, AmbientExecutionContext.Current.CancellationToken);
            Assert.Equal(options.Items, currentContext.Items);

        }
     
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public async Task MessageMediatr_Mediate_ShouldThrowInvalidOperationExceptionForNonRegisteredMessages()
    {
        
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        var mediator = new MessageMediator(
            registry,
            new MessageDependenciesFactory(null!)
        );

        var options = new MediateOptions<StubNonGenericMessage, Task>
        {
            RegisterPlainMessagesOnSpot = false,
            CancellationToken = CancellationToken.None,
            Items = new Dictionary<object, object?>(),
            MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry)!,
            MessageMediationStrategy = 
                new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>(),
            Groups = []
        };

        // Act & assert
        await Assert.ThrowsAsync<NoHandlerFoundException>(() => mediator
            .Mediate(new StubNonGenericDerivedMessage(), options));
        
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public async Task
        MessageMediatr_Mediate_ShouldThrowInvalidOperationExceptionForNonRegisteredMessagesWhenOnSpotActivated()
    {

        // Arrange
        var registry = new Mock<IMessageRegistry>();
        var mediationstrategy = new Mock<IMessageMediationStrategy<
            StubNonGenericMessage, Task>>();
        
        var messageResolveStrategy = new Mock<IMessageResolveStrategy>();
        
        var options = new MediateOptions<StubNonGenericMessage, Task>
        {
            MessageResolveStrategy = messageResolveStrategy.Object,
            MessageMediationStrategy = mediationstrategy.Object,
            RegisterPlainMessagesOnSpot = true,
            CancellationToken = CancellationToken.None,
            Groups = []
        };

        var mediator = new MessageMediator(registry.Object, new MessageDependenciesFactory(null));
        
        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Mediate(new StubNonGenericDerivedMessage(), options));
    }
}