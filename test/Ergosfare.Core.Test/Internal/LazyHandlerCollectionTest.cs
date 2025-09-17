using System.Collections;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Internal;

/// <summary>
/// Unit tests for <see cref="LazyHandlerCollection{THandler, TDescriptor}"/>.
/// </summary>
/// <remarks>
/// These tests verify construction, enumeration, and collection behavior
/// of <see cref="LazyHandlerCollection{THandler, TDescriptor}"/> using
/// a test fixture providing lazy handlers.
/// </remarks>
public class LazyHandlerCollectionTest(
    LazyHandlerFixture lazyHandlerFixture) : IClassFixture<LazyHandlerFixture>
{
    /// <summary>
    /// Verifies that a <see cref="LazyHandlerCollection{THandler, TDescriptor}"/>
    /// can be constructed and contains the expected lazy handler.
    /// </summary>
    [Fact]
    public void LazyHandlerCollectionShouldConstructedTest()
    {
        // Arrange: create a single-element lazy handler collection
        var collection = lazyHandlerFixture.CreateSingleElementLazyHandlerCollection<StubVoidHandler>();

        // Assert: collection is not null, contains exactly one item, and type is correct
        Assert.NotNull(collection);
        Assert.Single(collection);
        Assert.IsType<LazyHandlerCollection<StubVoidHandler, IHandlerDescriptor>>(collection, exactMatch: false);
    }

    /// <summary>
    /// Verifies that the generic enumerator of <see cref="LazyHandlerCollection{THandler, TDescriptor}"/>
    /// enumerates items correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void LazyHandlerCollectionShouldGetEnumeratorTest()
    {
        // Arrange: create a lazy handler
        var lazyHandler = lazyHandlerFixture.CreateLazyHandler<StubVoidHandler>();
        var collection = lazyHandlerFixture.CreateLazyHandlerCollection(lazyHandler);

        // Act: iterate using generic enumerator
        var enumerator = collection.GetEnumerator();

        // Assert: enumerator returns expected elements and stops correctly
        Assert.True(enumerator.MoveNext());
        Assert.Equal(enumerator.Current, lazyHandler);
        Assert.False(enumerator.MoveNext());

        // Cleanup
        enumerator.Dispose();
    }

    /// <summary>
    /// Verifies that the non-generic enumerator of <see cref="LazyHandlerCollection{THandler, TDescriptor}"/>
    /// works correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void LazyHandlerCollectionShouldGetNonGenericEnumeratorTest()
    {
        // Arrange
        var collection = lazyHandlerFixture.CreateSingleElementLazyHandlerCollection<StubVoidHandler>();
        var enumerable = (IEnumerable)collection;

        // Act
        var enumerator = enumerable.GetEnumerator();

        // Assert
        Assert.NotNull(enumerator);

        // Cleanup
        ((IDisposable)enumerator).Dispose();
    }
}

