using System.Collections;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Internal;

public class LazyHandlerCollectionTest
{

    [Fact]
    public void LazyHandlerCollectionShouldConstructedTest()
    {
        // arrange
        var lazyHandler = StubNonGenericLazyHandler.GetLazyInstance();
        // act
        var collection = new LazyHandlerCollection<StubNonGenericHandler,IHandlerDescriptor>(
            [ lazyHandler  ]);
        // assert
        Assert.NotNull(collection);
        Assert.Single(collection);
        Assert.IsType<LazyHandlerCollection<StubNonGenericHandler,IHandlerDescriptor>>(collection, exactMatch: false);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void LazyHandlerCollectionShouldGetEnumeratorTest()
    {
        // arrange
        var lazyHandler = StubNonGenericLazyHandler
            .GetLazyInstance();
        var collection = new LazyHandlerCollection<StubNonGenericHandler,IHandlerDescriptor>(
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
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void LazyHandlerCollectionShouldGetNonGenericEnumeratorTest()
    {
        // arrange
        var lazyHandler = StubNonGenericLazyHandler
            .GetLazyInstance();
        var collection = new LazyHandlerCollection<StubNonGenericHandler,IHandlerDescriptor>(
            [ lazyHandler  ]);
        var enumerable = (IEnumerable)collection;
        // act
        var enumerator = enumerable.GetEnumerator();
        // assert
        Assert.NotNull(enumerator);
        
        ((IDisposable)enumerator).Dispose();
    }
}