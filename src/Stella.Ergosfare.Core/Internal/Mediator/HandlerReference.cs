using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Process-wide handler reference. In the default mode every <see cref="Resolve"/> call
/// asks the dispatching scope's provider (from the execution context), so DI lifetimes
/// are honored per dispatch. In memoized mode the instance is resolved once from the
/// pinned provider (the root) and cached for the lifetime of the process.
/// </summary>
internal sealed class HandlerReference<THandler>(
    Type handlerType,
    IServiceProvider? memoizedProvider)
    : IHandlerReference<THandler>
{
    /// <summary>
    /// Cached instance; only ever set in memoized mode.
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

        // Losing the race is benign: everyone converges on the first published instance.
        existing = Interlocked.CompareExchange(ref _instance, created, null) ?? created;

        return (THandler) existing;
    }
}
