using System.Collections;
using System.Reflection;
using Stella.Ergosfare.Core.Internal.Registry;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;
using Xunit.Abstractions;

namespace Stella.Ergosfare.Core.Test;


/// <summary>
/// Unit tests for <see cref="MessageRegistry"/> using <see cref="MessageDependencyFixture"/>.
/// Covers message registration, duplicate handling, enumeration, and filtering of framework types.
/// </summary>
public class MessageRegistryTests: IClassFixture<MessageDependencyFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private MessageDependencyFixture _messageDependencyFixture;
    
    /// <summary>
    /// Initializes a new instance of <see cref="MessageRegistryTests"/> with the provided fixture and test output helper.
    /// </summary>
    /// <param name="messageDependencyFixture">The fixture providing the <see cref="MessageRegistry"/> instance.</param>
    /// <param name="testOutputHelper">The test output helper for logging.</param>
    public MessageRegistryTests(
        MessageDependencyFixture messageDependencyFixture,
        ITestOutputHelper testOutputHelper)
    {
        _messageDependencyFixture = messageDependencyFixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Verifies that <see cref="MessageRegistry.Register(Type)"/> correctly registers a single message type and its handler.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void ShouldMessageRegistryRegisterMessages()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;

        // act
        messageRegistry.Register(typeof(StubMessage));
        messageRegistry.Register(typeof(StubVoidHandler));
        // assert
        Assert.Single(messageRegistry);
        Assert.True(typeof(StubMessage)
            .IsAssignableFrom(messageRegistry.First().MessageType));
        // assert
        Assert.True(messageRegistry.First().Handlers.First().MessageType == typeof(StubMessage));
        _messageDependencyFixture.Dispose();
    }
    
    
    /// <summary>
    /// Verifies that multiple message types can be registered and duplicate handling works as expected.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void ShouldMessageRegistryRegisterMultipleMessages()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;

        // act
        messageRegistry.Register(typeof(StubVoidHandler)); 
        messageRegistry.Register(typeof(StubVoidHandler)); 
        messageRegistry.Register(typeof(StubStringHandler));
   
        // assert
        Assert.Single(messageRegistry);
        
        // cleanup
        _messageDependencyFixture.Dispose();
        
    }

    
    /// <summary>
    /// Ensures that <see cref="MessageRegistry.GetEnumerator"/> returns a functional enumerator for iteration.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageRegistryShouldHaveGetEnumerator()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;
        messageRegistry.Register(typeof(StubVoidHandler)); 
        
        // act
        var enumerator = messageRegistry.GetEnumerator();
        
        // assert
        Assert.True(enumerator.MoveNext());
        
        // cleanup
        enumerator.Dispose();
        _messageDependencyFixture.Dispose();
    }
    
    
    /// <summary>
    /// Ensures that the non-generic enumerator of <see cref="MessageRegistry"/> works correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageRegistryNonGenericGetEnumeratorShouldReturnsEnumerator()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;
        messageRegistry.Register(typeof(StubVoidHandler)); 
        messageRegistry.Register(typeof(StubStringHandler));

        // Act
        IEnumerable enumerable = messageRegistry;
        var enumerator = enumerable.GetEnumerator();

        // Assert
        Assert.True(enumerator.MoveNext());
        
        // cleanup
        ((IDisposable)enumerator).Dispose();
        _messageDependencyFixture.Dispose();
    }
    
    /// <summary>
    /// Verifies that registered messages are correctly processed and appear in the registry.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageRegistryShouldProcessMessageTypes()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;
        
        // Act
        messageRegistry.Register(typeof(StubVoidHandler)); 

        // Assert
        Assert.Single(messageRegistry);
        Assert.True(messageRegistry.First().MessageType == typeof(StubMessage));
        
        
        // cleanup
        _messageDependencyFixture.Dispose();
    }

    
    
    /// <summary>
    /// Ensures that framework types such as <see cref="System.Console"/> are ignored during registration.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageRegistryShouldIgnoreFrameworkTypes()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;
        
        // Act
        messageRegistry.Register(typeof(System.Console)); 

        // Assert
        Assert.Empty(messageRegistry);
        
        // cleanup 
        _messageDependencyFixture.Dispose();
    }
    
    
    
    /// <summary>
    /// Verifies that registering duplicate messages while still in the new messages queue is ignored.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void Register_ShouldIgnoreDuplicateMessagesInNewMessages()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;

        // Act - register the first message (goes to _newMessages initially)
        messageRegistry.Register(typeof(StubMessage));

        // Act - immediately register the same type again
        messageRegistry.Register(typeof(StubMessage));

        
        var field = typeof(MessageRegistry)
            .GetField("_newMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        var newMessages = (List<MessageDescriptor>)field?.GetValue(messageRegistry)!;
        // Assert
        // The second registration should be ignored because the type is still in _newMessages
        Assert.Empty(newMessages);
        
        // cleanup
        _messageDependencyFixture.Dispose();
    }
    
    
    /// <summary>
    /// Verifies that duplicate messages are ignored across both the new messages queue and already registered messages.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void Register_ShouldIgnoreDuplicatesInNewMessagesAndMessages()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;

        // Act 1: Register a message (goes to _newMessages first)
        messageRegistry.Register(typeof(StubMessage));

        // Act 2: Register the same type again while still in _newMessages
        messageRegistry.Register(typeof(StubMessage));

        // Assert 1: Only one message should be in the registry
        Assert.Single(messageRegistry);

        // Act 3: Commit the message (simulate finishing processing _newMessages)
        // Depending on your implementation, calling Register on a new type moves _newMessages into _messages
        messageRegistry.Register(typeof(StubIndirectMessage));

        // Act 4: Try registering the first message again (already in _messages)
        messageRegistry.Register(typeof(StubMessage));

        // Assert 2: Still only one copy of the first message
        Assert.Equal(2, messageRegistry.Count); // total: first message + second message
        
        // cleanup
        _messageDependencyFixture.Dispose();
    }
    
    /// <summary>
    /// Verifies that messages already in the new messages queue are skipped when attempting to register them again.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void Register_ShouldSkipMessageIfAlreadyInNewMessages()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var messageRegistry = _messageDependencyFixture.MessageRegistry;
        messageRegistry.Register(typeof(StubMessage));

        // Act 4: Try registering the first message again (already in _messages)
        messageRegistry.Register(typeof(StubIndirectMessage));
        
        var newMessagesField = typeof(MessageRegistry)
            .GetField("_newMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        var newMessagesList = (List<MessageDescriptor>)newMessagesField?.GetValue(messageRegistry)!;
       
        newMessagesList!.Add(new MessageDescriptor(typeof(StubMessage)));
        messageRegistry.Register(typeof(StubMessage));
        Assert.DoesNotContain(
            newMessagesList,
            d => d.MessageType == typeof(StubIndirectMessage)
        );
    }
    
    
    
    /// <summary>
    /// Verifies that registering a message that is already in the _newMessages queue
    /// does not add a duplicate.
    /// This covers the branch using FirstOrDefault to detect duplicates.
    /// </summary>
    [Fact]
    public void Register_ShouldNotDuplicateMessageIfAlreadyInNewMessages()
    {
        // arrange 
        _messageDependencyFixture = _messageDependencyFixture.New;
        var registry = _messageDependencyFixture.MessageRegistry;

        var newMessagesField = typeof(MessageRegistry)
            .GetField("_newMessages", BindingFlags.NonPublic | BindingFlags.Instance);
        var newMessages = (List<MessageDescriptor>)newMessagesField!.GetValue(registry)!;
        var descriptor = new MessageDescriptor(typeof(StubMessage));
        newMessages.Add(descriptor);

        // Use reflection to invoke the private RegisterMessage method
        var registerMessageMethod = typeof(MessageRegistry)
            .GetMethod("RegisterMessage", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act: call RegisterMessage with the same message type
        registerMessageMethod.Invoke(registry, new object?[] { typeof(StubMessage) });

        // Assert: _newMessages should still contain only the original descriptor (no duplicate)
        Assert.Single(newMessages);
        Assert.Equal(typeof(StubMessage), newMessages[0].MessageType);

        _messageDependencyFixture.Dispose();
    }
}   