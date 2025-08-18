using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class MessageDescriptorTests
{

    [Fact]
    public void MessageDescriptorShouldHaveMessageType()
    {

        // arrange & act
        var descriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
        
        // assert
        Assert.Equal(typeof(StubMessages.StubNonGenericMessage), descriptor.MessageType);
    }

    [Fact]
    public void MessageDescriptorShouldRegisterIndirectHandlers()
    {
        // arrange 
        var descriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericDerivedMessage));
        var descriptorFactory = new MessageDescriptorBuilderFactory();
        var indirectDescriptors = descriptorFactory.BuildDescriptors(typeof(StubHandlers.StubNonGenericHandler));
        
        // act 
        descriptor.AddDescriptors(indirectDescriptors);
        
        // assert
        
        Assert.NotEmpty(descriptor.IndirectHandlers);    

    }
    
    
    
    [Fact]
    public void MessageDescriptorShouldHandleGenericMessage()
    {
        // arrange &&  act 
        var genericDescriptor = new MessageDescriptor(typeof(StubMessages.StubGenericMessage<string>));
        var nonGenericDescriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
        // assert
        Assert.True(genericDescriptor.IsGeneric);    
        Assert.False(nonGenericDescriptor.IsGeneric);
    }
}