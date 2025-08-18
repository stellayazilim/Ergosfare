using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Test.__mocks__;
using Ergosfare.Test.__stubs__;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test.Strategies;

public class ActualTypeOrFirstAssignableTypeMessageResolveStrategyTest
(ITestOutputHelper  testOutputHelper)
{
    [Fact]
    public void MessageRegistryShouldResolveDescriptor()
    {
        
        // arrange
        var registry = new MessageRegistry(
            new MessageDescriptorBuilderFactory());
        
        registry.Register(typeof(StubHandlers.StubNonGenericHandler));
        registry.Register(typeof(StubHandlers.StubGenericHandler2));
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        
        // act
        var descriptor = resolver.Find(typeof(StubMessages.StubNonGenericMessage), registry);
        
        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(2, registry.Count);
        Assert.Equal(typeof(StubMessages.StubNonGenericMessage), descriptor?.MessageType);
        Assert.NotNull(descriptor?.Handlers.First( x => x.HandlerType == typeof(StubHandlers.StubNonGenericHandler)));

    }
    
    
    
    [Fact]
    
    public void MessageRegistryShouldFallbackToAssignableType()
    {
        // arrange
        var registry = new MessageRegistry(
            new MessageDescriptorBuilderFactory());
    
        registry.Register(typeof(StubHandlers.StubNonGenericHandler)); // handles BaseMessage
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
    
        // act
        var descriptor = resolver.Find(typeof(StubMessages.StubGenericDerivedMessage), registry);
    
        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubMessages.StubNonGenericMessage), descriptor?.MessageType);
    }
    
    
    [Fact]
    public void MessageRegistryShouldResolveGenericMessages()
    {
        // arrange
        var registry = new MessageRegistry(
            new MessageDescriptorBuilderFactory());
        
        // dummy generic string arg
        var mockGenericHandler = HandlerMocks.MockGenericHandler<string>();
        registry.Register(mockGenericHandler.Object.GetType()); // handles BaseMessage
        
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        
        testOutputHelper.WriteLine(registry.First().MessageType.ToString());
        // act
        var descriptor = resolver.Find(mockGenericHandler.Object.GetType(), registry);
        
        // assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(StubMessages.StubNonGenericMessage), descriptor?.MessageType);
    }
}