using System.Collections;
using Ergosfare.Messaging.Abstractions;
using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Internal;

public sealed class LazyHandlerCollection<THandler, TDescriptor>(
    IEnumerable<LazyHandler<THandler, TDescriptor>> source) : ILazyHandlerCollection<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    
    private readonly List<LazyHandler<THandler, TDescriptor>> _list = new(source);


    public IEnumerator<LazyHandler<THandler, TDescriptor>> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    public int Count => _list.Count;
}