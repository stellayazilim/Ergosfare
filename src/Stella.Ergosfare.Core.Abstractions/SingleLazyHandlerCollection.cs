using System.Collections;
using System.Collections.Generic;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions;

public sealed class SingleLazyHandlerCollection<THandler, TDescriptor> : ILazyHandlerCollection<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    private readonly ILazyHandler<THandler, TDescriptor> _handler;

    public SingleLazyHandlerCollection(ILazyHandler<THandler, TDescriptor> handler)
    {
        _handler = handler;
    }

    public int Count => 1;

    public ILazyHandler<THandler, TDescriptor> SingleHandler => _handler;

    public IEnumerator<ILazyHandler<THandler, TDescriptor>> GetEnumerator()
    {
        return new SingleEnumerator(_handler);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class SingleEnumerator(ILazyHandler<THandler, TDescriptor> handler) : IEnumerator<ILazyHandler<THandler, TDescriptor>>
    {
        private int _state = 0;
        public ILazyHandler<THandler, TDescriptor> Current => handler;
        object IEnumerator.Current => handler;

        public bool MoveNext()
        {
            if (_state == 0)
            {
                _state = 1;
                return true;
            }
            return false;
        }

        public void Reset() => _state = 0;
        public void Dispose() { }
    }
}
