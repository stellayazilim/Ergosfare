
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Represents a lazily initialized handler along with its descriptor.
/// </summary>
/// <typeparam name="THandler">The type of the handler.</typeparam>
/// <typeparam name="TDescriptor">The type of the handler descriptor.</typeparam>
public interface ILazyHandler<THandler, out TDescriptor>
{
    /// <summary>
    /// Gets the lazily initialized handler instance.
    /// </summary>
    public  Lazy<THandler> Handler { get; }
    
    /// <summary>
    /// Gets the descriptor associated with the handler.
    /// </summary>
    public TDescriptor Descriptor { get; }
}