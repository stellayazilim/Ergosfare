using System.Collections;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal;


/// <summary>
/// Represents a read-only, lazy collection of handlers with their associated descriptors.
/// </summary>
/// <typeparam name="THandler">The type of handler.</typeparam>
/// <typeparam name="TDescriptor">The type of handler descriptor.</typeparam>
/// <remarks>
/// Each element in the collection implements <see cref="ILazyHandler{THandler, TDescriptor}"/>,
/// allowing handlers to be instantiated lazily only when accessed.
/// </remarks>
public sealed class LazyHandlerCollection<THandler, TDescriptor> :
    ILazyHandlerCollection<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    /// <summary>
    /// Internal list storing the lazy handler entries.
    /// </summary>
    private readonly List<ILazyHandler<THandler, TDescriptor>> _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyHandlerCollection{THandler, TDescriptor}"/> class
    /// from the provided lazy handler source.
    /// </summary>
    /// <param name="source">The source collection of lazy handlers.</param>
    public LazyHandlerCollection(IEnumerable<ILazyHandler<THandler, TDescriptor>> source)
    {
        _list = new List<ILazyHandler<THandler, TDescriptor>>(source);
    }

    /// <summary>
    /// Gets the number of handlers in the collection.
    /// </summary>
    public int Count => _list.Count;

    /// <summary>
    /// Returns an enumerator that iterates through the collection of lazy handlers.
    /// </summary>
    /// <returns>An enumerator for <see cref="ILazyHandler{THandler, TDescriptor}"/>.</returns>
    public IEnumerator<ILazyHandler<THandler, TDescriptor>> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    /// <summary>
    /// Returns a non-generic enumerator that iterates through the collection.
    /// </summary>
    /// <returns>A non-generic <see cref="IEnumerator"/>.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
