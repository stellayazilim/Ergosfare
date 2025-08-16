using Ergosfare.Contracts;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
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

    private struct TestMessageRegistryMessage : IMessage;
    private struct TestMessageRegistryMessage2 : IMessage;
    
    private class TestMessageRegisterHandler: IAsyncHandler<TestMessageRegistryMessage>
    {
        public Task HandleAsync(TestMessageRegistryMessage message, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
    
    
    
    [Fact]
    public void ShouldMessageRegistryRegisterMessages()
    {
        // arrange 
        var messageRegistry = new MessageRegistry(new MessageDescriptorBuilderFactory());



        // act
        //messageRegistry.Register(typeof(TestMessageRegistryMessage));
        messageRegistry.Register(typeof(TestMessageRegisterHandler));
        // assert
        Assert.Single(messageRegistry);
        Assert.True(typeof(TestMessageRegistryMessage)
            .IsAssignableFrom(messageRegistry.First().MessageType));
        
        
        // assert
        //Assert.True(messageRegistry.First().Handlers.First().MessageType == typeof(TestMessageRegistryMessage));
    }
    
    [Fact]
    public void ShouldMessageRegistryRegisterMultipleMessages()
    {
        // arrange 
        var messageRegistry = new MessageRegistry(new MessageDescriptorBuilderFactory());



        // act
        
        messageRegistry.Register(typeof(TestMessageRegisterHandler)); // indirect register message

     
        //messageRegistry.Register(typeof(TestMessageRegistryMessage2));
   
        // assert
        
        _testOutputHelper.WriteLine(messageRegistry.First().MessageType.Name);
        _testOutputHelper.WriteLine(messageRegistry.First().Handlers.Count.ToString());
        _testOutputHelper.WriteLine(messageRegistry.First().IndirectHandlers.Count.ToString());
        Assert.Single(messageRegistry);
        
    }
}