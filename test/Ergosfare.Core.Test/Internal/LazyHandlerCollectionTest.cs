using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test.Internal;

public class LazyHandlerCollectionTest
{

    [Fact]
    public void LazyHandlerCollectionShouldConstructedTest()
    {

        
        // arrange
        var lazyHandler = HandlerStubs.StubLazyHandler;
        // act
        var collection = new LazyHandlerCollection<HandlerStubs.StubGenericHandler,IHandlerDescriptor>(
            [ HandlerStubs.StubLazyHandler  ]);
        
        // assert
        Assert.NotNull(collection);
        Assert.Single(collection);
        Assert.IsType<LazyHandlerCollection<HandlerStubs.StubGenericHandler,IHandlerDescriptor>>(collection, exactMatch: false);
    }

    [Fact]
    public void LazyHandlerCollectionShouldGetEnumeratorTest()
    {
        
        // arrange
  
        var lazyHandler = HandlerStubs.StubLazyHandler;
        var collection = new LazyHandlerCollection<HandlerStubs.StubGenericHandler,IHandlerDescriptor>(
            [ lazyHandler  ]);
        
        // act
        var enumerator = collection.GetEnumerator();
     
        Assert.NotNull(enumerator);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(enumerator.Current, lazyHandler);
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
      
    }
}