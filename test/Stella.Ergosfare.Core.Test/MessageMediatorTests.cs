using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Registry;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;
using Moq;
using Stella.Ergosfare.Core.Abstractions.Caching;
using Stella.Ergosfare.Core.Internal.Caching;

namespace Stella.Ergosfare.Core.Test;


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
            () =>  new MessageMediator(null!, null!, null!));
        Assert.Throws<ArgumentNullException>(
            () => new MessageMediator(registry, null!, null!));
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
        var messageDependencyFactory = new MessageDependenciesFactory(null!);
        // act
        var mediator = new MessageMediator(registry, messageDependencyFactory, new ServiceCollection().BuildServiceProvider());
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
        var messageDependencyFactory = new MessageDependenciesFactory(null!);
        var mediator = new MessageMediator(registry,messageDependencyFactory, new ServiceCollection().BuildServiceProvider());
        // act && assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            mediator
                .Mediate<StubMessage, ValueTask>(
                    new StubMessage(), null!).AsTask());
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
            new MessageDependenciesFactory(null!),
            new ServiceCollection().BuildServiceProvider()
        );
        var options = new MediateOptions<StubMessage, ValueTask>
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
            .Mediate(new StubIndirectMessage(), options).AsTask());
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
            StubMessage, ValueTask>>();
        var messageResolveStrategy = new Mock<IMessageResolveStrategy>();
        var options = new MediateOptions<StubMessage, ValueTask>
        {
            MessageResolveStrategy = messageResolveStrategy.Object,
            MessageMediationStrategy = mediationstrategy.Object,
            RegisterPlainMessagesOnSpot = true,
            CancellationToken = CancellationToken.None,
            Groups = []
        };
        var mediator = new MessageMediator(registry.Object, new MessageDependenciesFactory(null!), new ServiceCollection().BuildServiceProvider());
        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Mediate(new StubIndirectMessage(), options).AsTask());
    }
}