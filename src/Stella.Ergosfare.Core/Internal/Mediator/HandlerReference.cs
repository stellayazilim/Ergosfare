using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// A reference to one participant type, shared across dispatches.
/// </summary>
/// <typeparam name="THandler">The participant contract this reference resolves to.</typeparam>
/// <param name="handlerType">The participant type to resolve.</param>
/// <param name="memoizedProvider">
/// The provider to resolve from once and keep the instance of, or <c>null</c> to resolve
/// per dispatch.
/// </param>
/// <remarks>
/// By default every <see cref="Resolve"/> asks the provider the dispatcher passes in, so
/// registered lifetimes apply per dispatch. Given a memoized provider — the root — the
/// instance is resolved once and reused for the life of the process.
/// </remarks>
internal sealed class HandlerReference<THandler>(
    Type handlerType,
    IServiceProvider? memoizedProvider)
    : IHandlerReference<THandler>
{
    /// <summary>
    /// The kept instance; only ever set when memoizing.
    /// </summary>
    private object? _instance;

    /// <inheritdoc />
    public Type HandlerType { get; } = handlerType;

    /// <inheritdoc />
    public THandler Resolve(IServiceProvider serviceProvider)
    {
        if (memoizedProvider is null)
        {
            return (THandler) serviceProvider.GetRequiredService(HandlerType);
        }

        var existing = Volatile.Read(ref _instance);

        if (existing is not null)
        {
            return (THandler) existing;
        }

        var created = memoizedProvider.GetRequiredService(HandlerType);

        // Two threads may both construct one; whoever publishes first wins and both return
        // that instance, so callers never see two.
        existing = Interlocked.CompareExchange(ref _instance, created, null) ?? created;

        return (THandler) existing;
    }
}
