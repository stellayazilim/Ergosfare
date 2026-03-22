using System;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions;


/// <summary>
///     Represents a lazily initialized handler with its associated descriptor.
/// </summary>
/// <typeparam name="THandler">The type of the handler.</typeparam>
/// <typeparam name="TDescriptor">The type of the handler descriptor.</typeparam>
/// <remarks>
///     This structure allows for lazy initialization of handlers, which can improve performance
///     by deferring the creation of handler instances until they are actually needed.
/// </remarks>
public sealed class LazyHandler<THandler, TDescriptor>(Func<THandler> resolver, TDescriptor descriptor) : ILazyHandler<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    private THandler? _handler;

    public THandler Handler => _handler ??= resolver();

    /// <summary>
    ///     Gets the descriptor associated with the handler.
    /// </summary>
    public TDescriptor Descriptor => descriptor;
}