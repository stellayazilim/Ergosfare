using Ergosfare.Messaging.Abstractions;
using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Internal.Extensions;

public static class EnumerableExtensions
{
    public static ILazyHandlerCollection<THandler, TDescriptor> ToLazyReadOnlyCollection<THandler, TDescriptor>(this IEnumerable<LazyHandler<THandler, TDescriptor>> source)
        where TDescriptor : IHandlerDescriptor
    {
        return new LazyHandlerCollection<THandler, TDescriptor>(source);
    }
}