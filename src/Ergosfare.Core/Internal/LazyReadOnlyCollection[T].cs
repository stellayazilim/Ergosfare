using System.Collections;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal;

public sealed class LazyHandlerCollection<THandler, TDescriptor>(
    IEnumerable<ILazyHandler<THandler, TDescriptor>> source) : ILazyHandlerCollection<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    
    private readonly List<ILazyHandler<THandler, TDescriptor>> _list = new(source);


    public IEnumerator<ILazyHandler<THandler, TDescriptor>> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    public int Count => _list.Count;
}