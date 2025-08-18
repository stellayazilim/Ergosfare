using System.Collections;
using System.Reflection;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;
//using Telerik.JustMock;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test;

public class MessageRegistryTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MessageRegistryTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    
    [Fact]
    public void ShouldMessageRegistryRegisterMessages()
    {
        // arrange 
        var messageRegistry = new MessageRegistry(new MessageDescriptorBuilderFactory());



        // act
        //messageRegistry.Register(typeof(TestMessageRegistryMessage));
        messageRegistry.Register(typeof(StubHandlers.StubNonGenericHandler));
        // assert
        Assert.Single(messageRegistry);
        Assert.True(typeof(StubMessages.StubNonGenericMessage)
            .IsAssignableFrom(messageRegistry.First().MessageType));
        
        
        // assert
        //Assert.True(messageRegistry.First().Handlers.First().MessageType == typeof(TestMessageRegistryMessage));
    }
    
    [Fact]
    public void ShouldMessageRegistryRegisterMultipleMessages()
    {
        // arrange 
        var messageRegistry = new MessageRegistry(new MessageDescriptorBuilderFactory());

        // act
        messageRegistry.Register(typeof(StubHandlers.StubNonGenericHandler)); 
        messageRegistry.Register(typeof(StubHandlers.StubNonGenericHandler)); 
        messageRegistry.Register(typeof(StubHandlers.StubNonGenericHandlerDuplicate));
   
        // assert
        Assert.Single(messageRegistry);
        
    }

    [Fact]
    public void MessageRegistryShouldHaveGetEnumerator()
    {
        // arrange
        var messageRegistry = new MessageRegistry(new MessageDescriptorBuilderFactory());
        messageRegistry.Register(typeof(StubHandlers.StubNonGenericHandler)); 
        
        // act
        var enumerator = messageRegistry.GetEnumerator();
        
        // assert
        Assert.True(enumerator.MoveNext());
        
        // cleanup
        enumerator.Dispose();
    }
    
    
    [Fact]
    public void MessageRegistryNonGenericGetEnumeratorShouldReturnsEnumerator()
    {
        // Arrange
        var registry = new MessageRegistry(new MessageDescriptorBuilderFactory());
        registry.Register(typeof(StubHandlers.StubNonGenericHandler)); 
        registry.Register(typeof(StubHandlers.StubNonGenericHandlerDuplicate));

        // Act
        IEnumerable enumerable = (IEnumerable)registry;
        var enumerator = enumerable.GetEnumerator();

        // Assert
        Assert.True(enumerator.MoveNext());
        
        // cleanup
        ((IDisposable)enumerator).Dispose();
    }
    
    
    [Fact]
    public void MessageRegistryShouldProcessMessageTypes()
    {
        // Arrange
        var registry = new MessageRegistry(new MessageDescriptorBuilderFactory());
        
        // Act
        registry.Register(typeof(StubMessages.StubNonGenericMessage)); 

        // Assert
        Assert.Single(registry);
        Assert.True(registry.First().MessageType == typeof(StubMessages.StubNonGenericMessage));
    }

    
    
    [Fact]
    public void MessageRegistryShouldIgnoreFrameworkTypes()
    {
        // Arrange
        var registry = new MessageRegistry(new MessageDescriptorBuilderFactory());
        
        // Act
        registry.Register(typeof(System.Console)); 

        // Assert
        Assert.Empty(registry);
    }
    
    
    

    [Fact]
    public void Register_ShouldIgnoreDuplicateMessagesInNewMessages()
    {
        // Arrange
        var registry = new MessageRegistry(new MessageDescriptorBuilderFactory());

        // Act - register the first message (goes to _newMessages initially)
        registry.Register(typeof(StubMessages.StubNonGenericMessage));

        // Act - immediately register the same type again
        registry.Register(typeof(StubMessages.StubNonGenericMessage));

        
        var field = typeof(MessageRegistry)
            .GetField("_newMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        var newMessages = (List<MessageDescriptor>)field?.GetValue(registry)!;
        // Assert
        // The second registration should be ignored because the type is still in _newMessages
        Assert.Empty(newMessages);
        
    }
    
    
    
    [Fact]
    public void Register_ShouldIgnoreDuplicatesInNewMessagesAndMessages()
    {
        // Arrange
        var registry = new MessageRegistry(new MessageDescriptorBuilderFactory());

        // Act 1: Register a message (goes to _newMessages first)
        registry.Register(typeof(StubMessages.StubNonGenericMessage));

        // Act 2: Register the same type again while still in _newMessages
        registry.Register(typeof(StubMessages.StubNonGenericMessage));

        // Assert 1: Only one message should be in the registry
        Assert.Single(registry);

        // Act 3: Commit the message (simulate finishing processing _newMessages)
        // Depending on your implementation, calling Register on a new type moves _newMessages into _messages
        registry.Register(typeof(StubMessages.StubNonGenericMessage2));

        // Act 4: Try registering the first message again (already in _messages)
        registry.Register(typeof(StubMessages.StubNonGenericMessage));

        // Assert 2: Still only one copy of the first message
        Assert.Equal(2, registry.Count); // total: first message + second message
    }
    
    
    [Fact]
    public void Register_ShouldSkipMessageIfAlreadyInNewMessages()
    {
        // Arrange
        var registry = new MessageRegistry(new MessageDescriptorBuilderFactory());
        registry.Register(typeof(StubMessages.StubNonGenericMessage2));

        

        
        // Act 4: Try registering the first message again (already in _messages)
    
        registry.Register(typeof(StubMessages.StubNonGenericDerivedMessage));
        
        var newMessagesField = typeof(MessageRegistry)
            .GetField("_newMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        var newMessagesList = (List<MessageDescriptor>)newMessagesField?.GetValue(registry)!;
       
        newMessagesList!.Add(new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage)));
        registry.Register(typeof(StubMessages.StubNonGenericMessage));
        Assert.DoesNotContain(
            newMessagesList,
            d => d.MessageType == typeof(StubMessages.StubNonGenericDerivedMessage)
        );
    }
    
}   