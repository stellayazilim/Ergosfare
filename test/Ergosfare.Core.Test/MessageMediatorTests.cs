using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Test.Fixtures.Stubs.Basic;
using Moq;

namespace Ergosfare.Core.Test;


/// <summary>
/// Contains unit tests for <see cref="MessageMediator"/> ensuring proper construction,
/// error handling, and execution context management.
/// </summary>
public class MessageMediatorTests
{
    /// <summary>
    /// Tests that the <see cref="MessageMediator"/> constructor throws <see cref="ArgumentNullException"/>
    /// when required arguments are null.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageMediatorShouldThrowArgumentNullException()
    {       
        // arrange 
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        // act & assert
        Assert.Throws<ArgumentNullException>(
            () =>  new MessageMediator(null, new Core.Abstractions.SignalHub.SignalHub(),null));
        Assert.Throws<ArgumentNullException>(
            () => new MessageMediator(registry,  new Core.Abstractions.SignalHub.SignalHub(),null));
    }

    /// <summary>
    /// Tests that the <see cref="MessageMediator"/> can be constructed successfully with valid parameters.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageMediatrShouldConstructedTest()
    {
        // arrange 
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        var messageDependencyFactory = new MessageDependenciesFactory(null);
        // act
        var mediator = new MessageMediator(registry, new Core.Abstractions.SignalHub.SignalHub(), messageDependencyFactory);
        // assert
        Assert.NotNull(mediator);
    }
    
    /// <summary>
    /// Tests that <see cref="MessageMediator.Mediate{TMessage,TResult}"/> throws <see cref="ArgumentNullException"/>
    /// when options are null.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task MessageMediatr_Mediate_ShouldThrowArgumentNullException()
    {
        // arrange 
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        var messageDependencyFactory = new MessageDependenciesFactory(null);
        var mediator = new MessageMediator(registry,  new Core.Abstractions.SignalHub.SignalHub(),messageDependencyFactory);
        // act && assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            mediator
                .Mediate<StubMessage, Task>(
                    new StubMessage(), null));
    }
    
    /// <summary>
    /// Tests that <see cref="MessageMediator.Mediate{TMessage,TResult}"/> sets the <see cref="AmbientExecutionContext"/>
    /// correctly during mediation.
    /// </summary>    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Blackbox")]
    public void MessageMediatr_Mediate_ShouldSetExecutionContext()
    {
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        registry.Register(typeof(StubVoidHandler));
        registry.Register(typeof(StubPreInterceptor));
        //registry.Register(typeof(StubIndirectInterceptor));
        //registry.Register(typeof(StubNonGenericDerivedPreInterceptor));
        //registry.Register(typeof(StubNonGenericDerivedPreInterceptor2));
        registry.Register(typeof(StubPostInterceptor));
        //registry.Register(typeof(StubNonGenericPostInterceptor2));
        //registry.Register(typeof(StubNonGenericDerivedPostInterceptor));
        //registry.Register(typeof(StubNonGenericDerivedPostInterceptor2));
        var mediator = new MessageMediator(
            registry,
            new Core.Abstractions.SignalHub.SignalHub(),
            new MessageDependenciesFactory(null!)
        );
        var message = new StubMessage();
        var options = new MediateOptions<StubMessage, Task>
        {
            CancellationToken = CancellationToken.None,
            Items = new Dictionary<object, object?>(),
            MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry)!,
            MessageMediationStrategy = 
                new SingleAsyncHandlerMediationStrategy<StubMessage>(new ResultAdapterService()),
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

    /// <summary>
    /// Tests that <see cref="MessageMediator.Mediate{TMessage,TResult}"/> throws <see cref="NoHandlerFoundException"/>
    /// when attempting to mediate a message that is not registered and <c>RegisterPlainMessagesOnSpot</c> is false.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task MessageMediatr_Mediate_ShouldThrowInvalidOperationExceptionForNonRegisteredMessages()
    {
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        var mediator = new MessageMediator(
            registry,
            new Core.Abstractions.SignalHub.SignalHub(),
            new MessageDependenciesFactory(null!)
        );
        var options = new MediateOptions<StubMessage, Task>
        {
            RegisterPlainMessagesOnSpot = false,
            CancellationToken = CancellationToken.None,
            Items = new Dictionary<object, object?>(),
            MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry)!,
            MessageMediationStrategy = 
                new SingleAsyncHandlerMediationStrategy<StubMessage>(new ResultAdapterService()),
            Groups = []
        };
        // Act & assert
        await Assert.ThrowsAsync<NoHandlerFoundException>(() => mediator
            .Mediate(new StubIndirectMessage(), options));
    }

    /// <summary>
    /// Tests that <see cref="MessageMediator.Mediate{TMessage,TResult}"/> throws <see cref="InvalidOperationException"/>
    /// when mediating an unregistered message while <c>RegisterPlainMessagesOnSpot</c> is true and dependencies are mocked.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task
        MessageMediatr_Mediate_ShouldThrowInvalidOperationExceptionForNonRegisteredMessagesWhenOnSpotActivated()
    {
        // Arrange
        var registry = new Mock<IMessageRegistry>();
        var mediationstrategy = new Mock<IMessageMediationStrategy<
            StubMessage, Task>>();
        var messageResolveStrategy = new Mock<IMessageResolveStrategy>();
        var options = new MediateOptions<StubMessage, Task>
        {
            MessageResolveStrategy = messageResolveStrategy.Object,
            MessageMediationStrategy = mediationstrategy.Object,
            RegisterPlainMessagesOnSpot = true,
            CancellationToken = CancellationToken.None,
            Groups = []
        };
        var mediator = new MessageMediator(registry.Object, new Core.Abstractions.SignalHub.SignalHub(), new MessageDependenciesFactory(null));
        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Mediate(new StubIndirectMessage(), options));
    }
}