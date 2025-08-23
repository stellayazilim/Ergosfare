using System;

namespace Ergosfare.Core.Abstractions;

public interface ILazyHandler<THandler, out TDescriptor>
{
    public  Lazy<THandler> Handler { get; }
    
    public TDescriptor Descriptor { get; }
}