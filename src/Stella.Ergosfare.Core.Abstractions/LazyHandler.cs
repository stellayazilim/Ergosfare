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
public struct LazyHandler<THandler, TDescriptor>: ILazyHandler<THandler, TDescriptor>
    where TDescriptor : IHandlerDescriptor
{
    private THandler? _handler;
    private readonly Func<THandler> _resolver;

    public LazyHandler(Func<THandler> resolver, TDescriptor descriptor)
    {
        _resolver = resolver;
        Descriptor = descriptor;
        _handler = default;
    }

    public THandler Handler => _handler ??= _resolver();

    /// <summary>
    ///     Gets or initializes the descriptor associated with the handler.
    /// </summary>
    /// <remarks>
    ///     The descriptor provides metadata about the handler, such as the message type it handles,
    ///     its execution order, and any associated tags.
    /// </remarks>
    public TDescriptor Descriptor { get; }
}