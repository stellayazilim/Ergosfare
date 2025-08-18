using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class MessageDescriptorTests
{

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldHaveMessageType()
    {

        // arrange & act
        var descriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
        
        // assert
        Assert.Equal(typeof(StubMessages.StubNonGenericMessage), descriptor.MessageType);
    }

    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldRegisterHandlers()
    {
        // arrange
        var messgeDescriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
      
        var factory = new MessageDescriptorBuilderFactory();
        // act
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubHandlers.StubNonGenericHandler)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubHandlers.StubNonGenericPreInterceptor)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubHandlers.StubNonGenericPostInterceptor)));

        Assert.NotEmpty(messgeDescriptor.PreInterceptors);
        Assert.NotEmpty(messgeDescriptor.PostInterceptors);
        Assert.NotEmpty(messgeDescriptor.Handlers);
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldRegisterIndirectHandlers()
    {
        // arrange
        var messgeDescriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericDerivedMessage));
      
        var factory = new MessageDescriptorBuilderFactory();
        // act
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubHandlers.StubNonGenericHandler)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubHandlers.StubNonGenericPreInterceptor)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubHandlers.StubNonGenericPostInterceptor)));

        Assert.NotEmpty(messgeDescriptor.IndirectPreInterceptors);
        Assert.NotEmpty(messgeDescriptor.IndirectPostInterceptors);
        Assert.NotEmpty(messgeDescriptor.IndirectHandlers);

    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
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