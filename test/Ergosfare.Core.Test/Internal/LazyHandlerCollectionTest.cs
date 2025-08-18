using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test.Internal;

public class LazyHandlerCollectionTest
{

    [Fact]
    public void LazyHandlerCollectionShouldConstructedTest()
    {
        // arrange
        var lazyHandler = StubHandlers.StubLazyHandler;
        // act
        var collection = new LazyHandlerCollection<StubHandlers.StubNonGenericHandler,IHandlerDescriptor>(
            [ StubHandlers.StubLazyHandler  ]);
        // assert
        Assert.NotNull(collection);
        Assert.Single(collection);
        Assert.IsType<LazyHandlerCollection<StubHandlers.StubNonGenericHandler,IHandlerDescriptor>>(collection, exactMatch: false);
    }

    [Fact]
    public void LazyHandlerCollectionShouldGetEnumeratorTest()
    {
        // arrange
        var lazyHandler = StubHandlers.StubLazyHandler;
        var collection = new LazyHandlerCollection<StubHandlers.StubNonGenericHandler,IHandlerDescriptor>(
            [ lazyHandler  ]);
        // act
        var enumerator = collection.GetEnumerator();
        // assert
        Assert.NotNull(enumerator);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(enumerator.Current, lazyHandler);
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
      
    }
}