using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Registry;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;
using Stella.Ergosfare.Test.Fixtures.Stubs.Generic;

namespace Stella.Ergosfare.Core.Test.Strategies;

public class ActualTypeOrFirstAssignableTypeMessageResolveStrategyTest
{
    /// <summary>
    /// Ensures that the <see cref="MessageRegistry"/> can correctly resolve
    /// a <see cref="MessageDescriptor"/> for a registered message type,
    /// including its associated handlers.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageRegistryShouldResolveDescriptor()
    {
        // arrange
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory());

        // Register handlers for StubMessage and its indirect variant
        registry.Register(typeof(StubVoidHandler));          // handles StubMessage
        registry.Register(typeof(StubVoidIndirectHandler));  // handles StubIndirectMessage

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);

        // act
        var descriptor = resolver.Find(typeof(StubMessage));

        // assert
        Assert.NotNull(descriptor);

        // Descriptor should contain exactly one descriptor for StubMessage
        Assert.Single(descriptor.Handlers);

        // Descriptor should match the requested message type
        Assert.Equal(typeof(StubMessage), descriptor?.MessageType);

        // Verify that the expected handler is registered for StubMessage
        Assert.Contains(descriptor!.Handlers, h => h.HandlerType == typeof(StubVoidHandler));
    }
    
    
    /// <summary>
    /// Verifies that the <see cref="MessageRegistry"/> falls back to a handler
    /// registered for an assignable base type when no direct handler is registered
    /// for the requested message type.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageRegistryShouldFallbackToAssignableType()
    {
        // arrange
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory());

        // Register the base message and the handler that handles it
        registry.Register(typeof(StubMessage));
        registry.Register(typeof(StubVoidHandler)); // handles BaseMessage
        registry.Register(typeof(StubVoidIndirectHandler));

        // Also register the derived message
        registry.Register(typeof(StubIndirectMessage));

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);

        // act
        var descriptor = resolver.Find(typeof(StubMessage));

        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubMessage), descriptor?.MessageType);
    }
    
    
        /// <summary>
    /// Verifies that a generic message is correctly resolved to its generic type definition.
    /// </summary>
    [Fact]
    public void MessageRegistryShouldResolveGenericMessages()
    {
        // arrange
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory());
        
        // dummy generic string arg
        var mockGenericHandler = new VoidStubGenericHandler<string>();
        
        registry.Register(typeof(VoidStubGenericHandler<string>)); // handles BaseMessage
        
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
        
        // act
        var descriptor = resolver.Find(typeof(StubGenericMessage<string>));
        
        //assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubGenericMessage<>), descriptor?.MessageType);
    }

    /// <summary>
    /// Verifies that the resolver returns the descriptor when the exact type is registered in the registry.
    /// </summary>
    [Fact]
    public void Find_ShouldReturnDescriptor_WhenExactTypeRegistered()
    {
        // Arrange
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        registry.Register(typeof(StubMessage)); // exact type
        var strategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);

        // Act
        var descriptor = strategy.Find(typeof(StubMessage));

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubMessage), descriptor?.MessageType);
    }

    /// <summary>
    /// Verifies that the resolver returns a descriptor for an assignable base type
    /// when the exact type is not registered.
    /// </summary>
    [Fact]
    public void Find_ShouldReturnDescriptor_WhenAssignableTypeRegistered()
    {
        // Arrange
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        registry.Register(typeof(StubMessage)); // only base type registered
        var strategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);

        // Act
        var descriptor = strategy.Find(typeof(StubIndirectMessage)); // DerivedMessage : BaseMessage

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubMessage), descriptor?.MessageType);
    }

    /// <summary>
    /// Verifies that the resolver correctly resolves generic messages using the generic type definition.
    /// </summary>
    [Fact]
    public void Find_ShouldUseGenericTypeDefinition_WhenGenericTypeProvided()
    {
        // Arrange
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        registry.Register(typeof(StubGenericMessage<>));
        var strategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);

        // Act
        var descriptor = strategy.Find(typeof(StubGenericMessage<string>));

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubGenericMessage<>), descriptor?.MessageType);
    }
}