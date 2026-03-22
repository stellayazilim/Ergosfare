using System.Collections;
using System.Collections.Generic;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions;

public sealed class HandlerArrayCollection<THandler, TDescriptor> : ILazyHandlerCollection<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    private readonly ILazyHandler<THandler, TDescriptor>[] _items;

    public HandlerArrayCollection(ILazyHandler<THandler, TDescriptor>[] items)
    {
        _items = items;
    }

    public int Count => _items.Length;

    public ILazyHandler<THandler, TDescriptor> this[int index] => _items[index];

    public IEnumerator<ILazyHandler<THandler, TDescriptor>> GetEnumerator()
    {
        return ((IEnumerable<ILazyHandler<THandler, TDescriptor>>)_items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
