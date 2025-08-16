
namespace Ergosfare.Core.Test;

public class LazyHandlerTests
{
  
    [Fact]
    public void ShouldReturnLazyHandlerCollection()
    {
        // // arrange
        // var descriptor = new MainHandlerDescriptor()
        // {
        //     ResultType = typeof(Task),
        //     HandlerType = typeof(StubGenericHandler),
        //     MessageType = typeof(StubGenericMessage),
        // };
        //
        // IEnumerable<LazyHandler<StubGenericHandler, MainHandlerDescriptor>> enumerable =  new List<LazyHandler<StubGenericHandler, MainHandlerDescriptor>>()
        // {
        //     new LazyHandler<StubGenericHandler, MainHandlerDescriptor>()
        //     {
        //         Handler = new Lazy<StubGenericHandler>(() => new StubGenericHandler()),
        //         Descriptor = descriptor
        //     },
        // }.ToEnumerable();
        //
        // // act 
        // var collection = enumerable.ToLazyReadOnlyCollection();
        //
        // // assert
        // Assert.NotNull(collection);
        // Assert.Single(collection);
        // Assert.IsType<Lazy<StubGenericHandler>>(collection.First().Handler);
    }
}

