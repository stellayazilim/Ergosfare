using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Extensions;

public static class EnumerableExtensions
{
    public static ILazyHandlerCollection<THandler, TDescriptor> ToLazyReadOnlyCollection<THandler, TDescriptor>(this IEnumerable<ILazyHandler<THandler, TDescriptor>> source)
        where TDescriptor : IHandlerDescriptor
    {
        return new LazyHandlerCollection<THandler, TDescriptor>(source);
    }
}