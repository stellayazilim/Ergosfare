using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Common;
public class MessageDescriptorTests
{

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldHaveMessageType()
    {

        // arrange & act
        var descriptor = new MessageDescriptor(typeof(StubNonGenericMessage));
        
        // assert
        Assert.Equal(typeof(StubNonGenericMessage), descriptor.MessageType);
    }

    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldRegisterHandlers()
    {
        // arrange
        var messgeDescriptor = new MessageDescriptor(typeof(StubNonGenericMessage));
      
        var factory = new HandlerDescriptorBuilderFactory();
        // act
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericHandler)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericPreInterceptor)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericPostInterceptor)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericExceptionInterceptor)));
        Assert.NotEmpty(messgeDescriptor.PreInterceptors);
        Assert.NotEmpty(messgeDescriptor.PostInterceptors);
        Assert.NotEmpty(messgeDescriptor.Handlers);
        Assert.NotEmpty(messgeDescriptor.ExceptionInterceptors);
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldRegisterIndirectHandlers()
    {
        // arrange
        var messgeDescriptor = new MessageDescriptor(typeof(StubNonGenericDerivedMessage));
      
        var factory = new HandlerDescriptorBuilderFactory();
        // act
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericHandler)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericPreInterceptor)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericPostInterceptor)));
        messgeDescriptor.AddDescriptors(factory.BuildDescriptors(typeof(StubNonGenericExceptionInterceptor)));
        
        Assert.NotEmpty(messgeDescriptor.IndirectPreInterceptors);
        Assert.NotEmpty(messgeDescriptor.IndirectPostInterceptors);
        Assert.NotEmpty(messgeDescriptor.IndirectHandlers);
        Assert.NotEmpty(messgeDescriptor.IndirectExceptionInterceptors);
        //Assert.NotEmpty(messgeDescriptor.Handlers);

    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldHandleGenericMessage()
    {
        // arrange &&  act 
        var genericDescriptor = new MessageDescriptor(typeof(StubGenericMessage<string>));
        var nonGenericDescriptor = new MessageDescriptor(typeof(StubNonGenericMessage));
        // assert
        Assert.True(genericDescriptor.IsGeneric);    
        Assert.False(nonGenericDescriptor.IsGeneric);
    }



}