using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's streaming pipeline for one (query, result) pair, seen through its result
/// type so the table can hand it back without naming the query type.
/// </summary>
internal interface IStreamDispatch<out TResult>
{
    IAsyncEnumerable<TResult> Stream(
        object query,
        ErgosfareContext? context,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups);
}

/// <summary>The typed closure of <see cref="IStreamDispatch{TResult}"/>.</summary>
/// <remarks>
/// Belongs to a container, like the broadcast and executor pipelines beside it. The streaming
/// lane used to be one object per (query, result) pair for the whole process, carrying the
/// last serving container's factory as part of its cache key; ownership says that
/// structurally and the guard goes away.
/// </remarks>
internal sealed class StreamDispatch<TQuery, TResult>(IMessageDependenciesFactory dependenciesFactory)
    : IStreamDispatch<TResult>
    where TQuery : notnull
{
    // One copy per closed query type is deliberate — the dispatch itself is per type.
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string[] EmptyGroups = [];

    private IMessageDependencies? _cachedDependencies;
    private GroupedSlot? _cachedGroupedSlot;

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> Stream(
        object query,
        ErgosfareContext? context,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        var dependencies = groups is null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }
            ? GetDependencies()
            : GetGroupedDependencies(groups);

        // A fresh, unpooled context when the caller supplied none: enumeration happens after
        // this call returns, so its completion is not observable here and the context cannot go
        // back to the pool either way.
        context ??= new ErgosfareContext(cancellationToken: cancellationToken);
        var strategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(cancellationToken);

        return strategy.Mediate((TQuery)query, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            return Build(dependenciesFactory, EmptyGroups);
        }

        var cached = _cachedDependencies;

        if (cached is not null)
        {
            return cached;
        }

        var dependencies = Build(typedFactory, EmptyGroups);
        _cachedDependencies = dependencies;
        return dependencies;
    }

    /// <summary>
    /// Grouped counterpart of <see cref="GetDependencies"/>: a single last-used group-set slot
    /// with an ordinal element-wise compare, snapshotting the caller's sequence so later
    /// mutation of a reused settings instance reads as a different group set.
    /// </summary>
    private IMessageDependencies GetGroupedDependencies(IEnumerable<string> groups)
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            return Build(dependenciesFactory, [.. groups]);
        }

        var slot = _cachedGroupedSlot;

        // Deliberate: groups is matched allocation-free first and only materialized on a miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is not null && GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            return slot.Dependencies;
        }

        var canonical = groups as GroupSet;
        // ReSharper disable once PossibleMultipleEnumeration
        string[] materialized = [.. groups];
        var dependencies = Build(typedFactory, materialized);
        _cachedGroupedSlot = new GroupedSlot(materialized, canonical, dependencies);
        return dependencies;
    }

    private static IMessageDependencies Build(IMessageDependenciesFactory factory, string[] groups)
        // A query with no composition is a failed dispatch, not an empty stream: Create throws
        // NoHandlerFoundException exactly as the descriptor lookup did.
        => factory.Create(typeof(TQuery), groups);

    private sealed class GroupedSlot(string[] groups, GroupSet? canonical, IMessageDependencies dependencies)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
    }
}
