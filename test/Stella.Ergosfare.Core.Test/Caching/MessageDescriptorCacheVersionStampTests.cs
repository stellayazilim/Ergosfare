using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Caching;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Caching;

namespace Stella.Ergosfare.Core.Test.Caching;

/// <summary>
/// The dependency cache's version stamps close a check-clear-add race: a dependency build
/// that read the registry at version N can finish — and store its result — after another
/// dispatch already invalidated the cache for version N+1. Before the stamps, that late
/// add was served to every subsequent dispatch as a fresh entry, freezing a stale
/// pipeline until the next (possibly never-coming) registry bump. These tests replay the
/// interleaving deterministically through the cache API.
/// </summary>
public class MessageDescriptorCacheVersionStampTests
{
    private sealed class NullCacheStrategy : IDescriptorCacheStrategy
    {
        public bool TryGet(string key, out object? value)
        {
            value = null;
            return false;
        }

        public void Add(string key, object value)
        {
        }

        public void Clear()
        {
        }

        public void Evict(string key)
        {
        }

        public int Count => 0;

        public void Dispose()
        {
        }
    }

    private sealed class StubDependencies : IMessageDependencies
    {
        public IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> Handlers => [];
        public IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> IndirectHandlers => [];
        public IReadOnlyList<IHandlerReference<IPreInterceptor, IPreInterceptorDescriptor>> PreInterceptors => [];
        public IReadOnlyList<IHandlerReference<IPostInterceptor, IPostInterceptorDescriptor>> PostInterceptors => [];
        public IReadOnlyList<IHandlerReference<IExceptionInterceptor, IExceptionInterceptorDescriptor>> ExceptionInterceptors => [];
        public IReadOnlyList<IHandlerReference<IFinalInterceptor, IFinalInterceptorDescriptor>> FinalInterceptors => [];
    }

    private sealed class ProbeMessage;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void LateAddFromAnOlderVersion_IsInvisibleToTheNewVersion()
    {
        var cache = new MessageDescriptorCache(new NullCacheStrategy());
        var staleDependencies = new StubDependencies();

        // Dispatch A reads registry version 1 and starts building.
        cache.InvalidateIfRegistryChanged(1);

        // A registration bumps the registry; dispatch B invalidates for version 2.
        cache.InvalidateIfRegistryChanged(2);

        // Dispatch A's build finishes late and stores its version-1 result.
        cache.AddDependencies(typeof(ProbeMessage), [], staleDependencies, 1);

        // A version-2 reader must rebuild, not serve the stale entry.
        Assert.False(cache.TryGetDependencies(typeof(ProbeMessage), [], 2, out _));

        // The same interleaving through the grouped store.
        cache.AddDependencies(typeof(ProbeMessage), ["reporting"], staleDependencies, 1);
        Assert.False(cache.TryGetDependencies(typeof(ProbeMessage), ["reporting"], 2, out _));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MatchingVersion_ServesTheCachedEntry()
    {
        var cache = new MessageDescriptorCache(new NullCacheStrategy());
        var dependencies = new StubDependencies();

        cache.InvalidateIfRegistryChanged(1);
        cache.AddDependencies(typeof(ProbeMessage), [], dependencies, 1);

        Assert.True(cache.TryGetDependencies(typeof(ProbeMessage), [], 1, out var cached));
        Assert.Same(dependencies, cached);
    }
}
