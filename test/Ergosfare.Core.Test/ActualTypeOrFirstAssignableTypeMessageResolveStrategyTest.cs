using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class ActualTypeOrFirstAssignableTypeMessageResolveStrategyTest
{
    [Fact]
    public void MessageRegistryShouldResolveDescriptor()
    {
        
        // arrange
        var registry = new MessageRegistry(
            new MessageDescriptorBuilderFactory());
        
        registry.Register(typeof(HandlerStubs.StubGenericHandler));
        registry.Register(typeof(HandlerStubs.StubGenericHandler2));
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        
        // act
        var descriptor = resolver.Find(typeof(HandlerStubs.StubGenericMessage), registry);
        
        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(2, registry.Count);
        Assert.Equal(typeof(HandlerStubs.StubGenericMessage), descriptor?.MessageType);
        Assert.NotNull(descriptor?.Handlers.First( x => x.HandlerType == typeof(HandlerStubs.StubGenericHandler)));

    }
    
    
    
    [Fact]
    
    public void MessageRegistryShouldFallbackToAssignableType()
    {
        // arrange
        var registry = new MessageRegistry(
            new MessageDescriptorBuilderFactory());
    
        registry.Register(typeof(HandlerStubs.StubGenericHandler)); // handles BaseMessage
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
    
        // act
        var descriptor = resolver.Find(typeof(HandlerStubs.StubGenericDerivedMessage), registry);
    
        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(HandlerStubs.StubGenericMessage), descriptor?.MessageType);
    }
}