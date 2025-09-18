using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;
using Ergosfare.Test.Fixtures.Stubs.Generic;

namespace Ergosfare.Core.Test.Common;


/// <summary>
/// Unit tests for <see cref="IMessageDescriptor"/> implementations using <see cref="DescriptorFixture"/>.
/// Verifies that message descriptors correctly expose their metadata, including message types and registered handlers.
/// </summary>
public class MessageDescriptorTests(
    DescriptorFixture descriptorFixture) : BaseDescriptorFixture(descriptorFixture)
{
    
    /// <summary>
    /// Ensures that <see cref="DescriptorFixture.CreateMessageDescriptor{TMessage}"/>
    /// produces a descriptor with the correct <see cref="IMessageDescriptor.MessageType"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldHaveMessageType()
    {
        // arrange 
        var descriptor = DescriptorFixture.CreateMessageDescriptor<StubMessage>();
        // act
        var messageType = descriptor.MessageType;
        // assert
        Assert.Equal(typeof(StubMessage), messageType);
    }

    

    /// <summary>
    /// Verifies that a message descriptor correctly registers direct main handlers and interceptors.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldHaveRegisteredDirectHandlers()
    {
        // arrange 
        var descriptor = (MessageDescriptor)DescriptorFixture.CreateMessageDescriptor<StubMessage>();

        var main = DescriptorFixture.CreateMainDescriptor<StubMessage, object, StubVoidHandler>();
        var pre = DescriptorFixture.CreatePreDescriptor<StubMessage, StubPreInterceptor>();
        var post = DescriptorFixture.CreatePostDescriptor<StubMessage, object, StubPostInterceptor>();
        var exception = DescriptorFixture.CreateExceptionDescriptor<StubMessage, object, StubExceptionInterceptor>();
        var final = DescriptorFixture.CreateFinalDescriptor<StubMessage, object, StubFinalInterceptor>();
        
        // act  
        descriptor.AddDescriptor(main);
        descriptor.AddDescriptor(pre);
        descriptor.AddDescriptor(post);
        descriptor.AddDescriptor(exception);
        descriptor.AddDescriptor(final);
        
        // assert
        Assert.Same(main, descriptor.Handlers.FirstOrDefault());
        Assert.Same(pre, descriptor.PreInterceptors.FirstOrDefault());
        Assert.Same(post, descriptor.PostInterceptors.FirstOrDefault());
        Assert.Same(exception, descriptor.ExceptionInterceptors.FirstOrDefault());
        Assert.Same(final, descriptor.FinalInterceptors.FirstOrDefault());
    }
    /// <summary>
    /// Verifies that a message descriptor correctly registers handlers and interceptors for indirect messages.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldRegisterIndirectHandlers()
    {
        // arrange 
        var descriptor = (MessageDescriptor)DescriptorFixture.CreateMessageDescriptor<StubIndirectMessage>();

        var main = DescriptorFixture.CreateMainDescriptor<StubMessage, object, StubVoidHandler>();
        var pre = DescriptorFixture.CreatePreDescriptor<StubMessage, StubPreInterceptor>();
        var post = DescriptorFixture.CreatePostDescriptor<StubMessage, object, StubPostInterceptor>();
        var exception = DescriptorFixture.CreateExceptionDescriptor<StubMessage, object, StubExceptionInterceptor>();
        var final = DescriptorFixture.CreateFinalDescriptor<StubMessage, object, StubFinalInterceptor>();
        
        // act  
        descriptor.AddDescriptor(main);
        descriptor.AddDescriptor(pre);
        descriptor.AddDescriptor(post);
        descriptor.AddDescriptor(exception);
        descriptor.AddDescriptor(final);
        
        // assert
        Assert.Same(main, descriptor.IndirectHandlers.FirstOrDefault());
        Assert.Same(pre, descriptor.IndirectPreInterceptors.FirstOrDefault());
        Assert.Same(post, descriptor.IndirectPostInterceptors.FirstOrDefault());
        Assert.Same(exception, descriptor.IndirectExceptionInterceptors.FirstOrDefault());
        Assert.Same(final, descriptor.IndirectFinalInterceptors.FirstOrDefault());
    }
    
    
    /// <summary>
    /// Checks that the message descriptor correctly identifies generic and non-generic message types.
    /// </summary>

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorShouldHandleGenericMessage()
    {
        // arrange && act
        var descriptor = DescriptorFixture.CreateMessageDescriptor<StubMessage>().IsGeneric;
        var genericDescriptor = DescriptorFixture.CreateMessageDescriptor<StubGenericMessage<string>>().IsGeneric;
        
        // assert
        Assert.False(descriptor);
        Assert.True(genericDescriptor);    
       
    }
}