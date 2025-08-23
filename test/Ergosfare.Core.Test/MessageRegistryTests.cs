using System.Collections;
using System.Reflection;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;
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
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());



        // act
        //messageRegistry.Register(typeof(TestMessageRegistryMessage));
        messageRegistry.Register(typeof(StubNonGenericHandler));
        // assert
        Assert.Single(messageRegistry);
        Assert.True(typeof(StubNonGenericMessage)
            .IsAssignableFrom(messageRegistry.First().MessageType));
        
        
        // assert
        //Assert.True(messageRegistry.First().Handlers.First().MessageType == typeof(TestMessageRegistryMessage));
    }
    
    [Fact]
    public void ShouldMessageRegistryRegisterMultipleMessages()
    {
        // arrange 
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        // act
        messageRegistry.Register(typeof(StubNonGenericHandler)); 
        messageRegistry.Register(typeof(StubNonGenericHandler)); 
        messageRegistry.Register(typeof(StubNonGenericHandler2));
   
        // assert
        Assert.Single(messageRegistry);
        
    }

    [Fact]
    public void MessageRegistryShouldHaveGetEnumerator()
    {
        // arrange
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        messageRegistry.Register(typeof(StubNonGenericHandler)); 
        
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
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        registry.Register(typeof(StubNonGenericHandler)); 
        registry.Register(typeof(StubNonGenericHandler2));

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
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        // Act
        registry.Register(typeof(StubNonGenericMessage)); 

        // Assert
        Assert.Single(registry);
        Assert.True(registry.First().MessageType == typeof(StubNonGenericMessage));
    }

    
    
    [Fact]
    public void MessageRegistryShouldIgnoreFrameworkTypes()
    {
        // Arrange
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        // Act
        registry.Register(typeof(System.Console)); 

        // Assert
        Assert.Empty(registry);
    }
    
    
    

    [Fact]
    public void Register_ShouldIgnoreDuplicateMessagesInNewMessages()
    {
        // Arrange
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        // Act - register the first message (goes to _newMessages initially)
        registry.Register(typeof(StubNonGenericMessage));

        // Act - immediately register the same type again
        registry.Register(typeof(StubNonGenericMessage));

        
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
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        // Act 1: Register a message (goes to _newMessages first)
        registry.Register(typeof(StubNonGenericMessage));

        // Act 2: Register the same type again while still in _newMessages
        registry.Register(typeof(StubNonGenericMessage));

        // Assert 1: Only one message should be in the registry
        Assert.Single(registry);

        // Act 3: Commit the message (simulate finishing processing _newMessages)
        // Depending on your implementation, calling Register on a new type moves _newMessages into _messages
        registry.Register(typeof(StubNonGenericMessage2));

        // Act 4: Try registering the first message again (already in _messages)
        registry.Register(typeof(StubNonGenericMessage));

        // Assert 2: Still only one copy of the first message
        Assert.Equal(2, registry.Count); // total: first message + second message
    }
    
    
    [Fact]
    public void Register_ShouldSkipMessageIfAlreadyInNewMessages()
    {
        // Arrange
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        registry.Register(typeof(StubNonGenericMessage2));

        

        
        // Act 4: Try registering the first message again (already in _messages)
    
        registry.Register(typeof(StubNonGenericDerivedMessage));
        
        var newMessagesField = typeof(MessageRegistry)
            .GetField("_newMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        var newMessagesList = (List<MessageDescriptor>)newMessagesField?.GetValue(registry)!;
       
        newMessagesList!.Add(new MessageDescriptor(typeof(StubNonGenericMessage)));
        registry.Register(typeof(StubNonGenericMessage));
        Assert.DoesNotContain(
            newMessagesList,
            d => d.MessageType == typeof(StubNonGenericDerivedMessage)
        );
    }
    
}   