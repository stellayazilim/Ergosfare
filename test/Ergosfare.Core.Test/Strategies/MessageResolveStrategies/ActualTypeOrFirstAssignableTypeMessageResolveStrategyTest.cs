using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Strategies;

public class ActualTypeOrFirstAssignableTypeMessageResolveStrategyTest
{
    [Fact]
    public void MessageRegistryShouldResolveDescriptor()
    {
        // arrange
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory());
        
        registry.Register(typeof(StubNonGenericHandler));
        registry.Register(typeof(StubNonGenericHandler2));
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        
        // act
        var descriptor = resolver.Find(typeof(StubNonGenericMessage), registry);
        
        // assert
        Assert.NotNull(descriptor);
        Assert.Single(registry);
        Assert.Equal(typeof(StubNonGenericMessage), descriptor?.MessageType);
        Assert.NotNull(descriptor?.Handlers.First( x => x.HandlerType == typeof(StubNonGenericHandler)));

    }
    
    
    
    [Fact]
    
    public void MessageRegistryShouldFallbackToAssignableType()
    {
        // arrange
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory());
    
        registry.Register(typeof(StubNonGenericHandler)); // handles BaseMessage
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
    
        // act
        var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage), registry);
    
        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubNonGenericMessage), descriptor?.MessageType);
    }
    
    
    [Fact]
    public void MessageRegistryShouldResolveGenericMessages()
    {
        // arrange
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory());
        
        // dummy generic string arg
        var mockGenericHandler = new StubGenericHandler<string>();
        
        registry.Register(typeof(StubGenericHandler<string>)); // handles BaseMessage
        
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        
        // act
        var descriptor = resolver.Find(typeof(StubGenericMessage<string>), registry);
        
        //assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubGenericMessage<>), descriptor?.MessageType);
    }
}