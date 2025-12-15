using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IEnumerable{T}"/> to convert to lazy handler collections.
/// </summary>
public static class EnumerableExtensions
{
    
    /// <summary>
    /// Converts an <see cref="IEnumerable{ILazyHandler}"/> into an <see cref="ILazyHandlerCollection{THandler, TDescriptor}"/>.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    /// <typeparam name="TDescriptor">The type of handler descriptor, must implement <see cref="IHandlerDescriptor"/>.</typeparam>
    /// <param name="source">The source enumerable of lazy handlers.</param>
    /// <returns>A lazy handler collection wrapping the source enumerable.</returns>
    public static ILazyHandlerCollection<THandler, TDescriptor> ToLazyReadOnlyCollection<THandler, TDescriptor>(this IEnumerable<ILazyHandler<THandler, TDescriptor>> source)
        where TDescriptor : IHandlerDescriptor
    {
        return new LazyHandlerCollection<THandler, TDescriptor>(source);
    }
}