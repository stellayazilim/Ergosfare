

namespace Ergosfare.Core.Test.Strategies;


public class SingleAsyncHandlerMediationStrategyTMessageTests
{
    public record TestMessage : IMessage;

    
    [Fact]
    public async Task ShouldInvokeSingleAsyncHandlerWithoutResponse()
    {
        // // Arrange
        // var message = Mock.Create<IMessage>();
        // var descriptor = new HandlerDescriptorBuilder();
        // var handlerMock = Mock.Create<IHandler>();
        // var descriptorMock = Mock.Create<IMainHandlerDescriptor>();
        //
        // var lazyHandler = new LazyHandler<IHandler, IMainHandlerDescriptor>
        // {
        //     Handler = new Lazy<IHandler>(() => handlerMock),
        //     Descriptor = descriptorMock
        // };
        //
        // // Mock the handler collection
        // var handlerCollectionMock = Mock.Create<ILazyHandlerCollection<IHandler, IMainHandlerDescriptor>>();
        //
        // // Arrange enumerator
        // Mock.Arrange(() => handlerCollectionMock.GetEnumerator())
        //     .Returns(new List<LazyHandler<IHandler, IMainHandlerDescriptor>> { lazyHandler }.GetEnumerator());
        //
        // // Arrange Count
        // Mock.Arrange(() => handlerCollectionMock.Count)
        //     .Returns(1);
        //
        // var deps = Mock.Create<IMessageDependencies>();
        //
        // // Return the mocked collection instead of a raw list
        // Mock.Arrange(() => deps.Handlers)
        //     .Returns(handlerCollectionMock);
        //
        // var context = Mock.Create<IExecutionContext>();
        //
        // Mock.Arrange(() => handlerMock.Handle(message))
        //     .Returns(Task.CompletedTask)
        //     .OccursOnce();
        //
        // var strategy = new SingleAsyncHandlerMediationStrategy<IMessage>();
        //
        // // Act
        // await strategy.Mediate(message, deps, context);
        //
        // // Assert
        // Mock.Assert(handlerMock);
    }
}
