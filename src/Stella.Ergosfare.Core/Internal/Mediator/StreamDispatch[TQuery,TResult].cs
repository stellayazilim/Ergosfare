using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's streaming pipeline for a (query, item) pair, seen through its item type
/// so the table can return it without naming the query type.
/// </summary>
/// <typeparam name="TResult">The type of the streamed items.</typeparam>
internal interface IStreamDispatch<out TResult>
{
    /// <summary>
    /// Streams the results of <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The query to run.</param>
    /// <param name="context">
    /// The caller's execution context, or <c>null</c> to create one for this stream.
    /// </param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups to run, or <c>null</c> for the default.</param>
    /// <returns>The streamed results.</returns>
    IAsyncEnumerable<TResult> Stream(
        object query,
        ErgosfareContext? context,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups);
}

/// <summary>
/// <see cref="IStreamDispatch{TResult}"/> closed over its query and item types.
/// </summary>
/// <typeparam name="TQuery">The query type this pipeline serves.</typeparam>
/// <typeparam name="TResult">The type of the streamed items.</typeparam>
/// <param name="dependenciesFactory">The factory participants are resolved through.</param>
/// <remarks>
/// Like the publish and send pipelines beside it, this belongs to one container.
/// </remarks>
internal sealed class StreamDispatch<TQuery, TResult>(IMessageDependenciesFactory dependenciesFactory)
    : IStreamDispatch<TResult>
    where TQuery : notnull
{
    // One copy per closed query type is deliberate — the dispatch itself is per type.
    // ReSharper disable once StaticMemberInGenericType

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

        // A context created here is never recycled: enumeration happens after this call
        // returns, so there is no point at which the context is known to be finished with.
        context ??= new ErgosfareContext(cancellationToken: cancellationToken);
        var strategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(cancellationToken);

        return strategy.Mediate((TQuery)query, dependencies, context, serviceProvider);
    }

    /// <summary>
    /// Returns the participants of the ungrouped pipeline, resolving them on first use.
    /// </summary>
    /// <returns>The participants for this query.</returns>
    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            return Build(dependenciesFactory, []);
        }

        var cached = _cachedDependencies;

        if (cached is not null)
        {
            return cached;
        }

        var dependencies = Build(typedFactory, []);
        _cachedDependencies = dependencies;
        return dependencies;
    }

    /// <summary>
    /// Returns the participants a group set selects, behind a single last-used slot.
    /// </summary>
    /// <param name="groups">The groups the caller asked for.</param>
    /// <returns>The participants for that set.</returns>
    /// <remarks>
    /// The caller's sequence is compared without being copied and copied only on a miss,
    /// and the copy is what the participants are built from — so mutating a reused sequence
    /// afterwards reads as a different group set rather than silently changing this one.
    /// </remarks>
    private IMessageDependencies GetGroupedDependencies(IEnumerable<string> groups)
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            return Build(dependenciesFactory, [.. groups]);
        }

        var slot = _cachedGroupedSlot;

        // Deliberate: groups is compared without being copied first, and copied only on a
        // miss.
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

    /// <summary>
    /// Builds the participants for one group set.
    /// </summary>
    /// <param name="factory">The factory to build through.</param>
    /// <param name="groups">The groups to filter participants by.</param>
    /// <returns>The participants for this query.</returns>
    /// <exception cref="Abstractions.Exceptions.NoHandlerFoundException">
    /// No composition serves the query. A query nothing handles is a failed dispatch rather
    /// than an empty stream, which is why this throws instead of returning nothing.
    /// </exception>
    private static IMessageDependencies Build(IMessageDependenciesFactory factory, string[] groups)
        => factory.Create(typeof(TQuery), groups);

    /// <summary>
    /// One group set and the participants it selects.
    /// </summary>
    /// <param name="groups">The group names this slot was built for.</param>
    /// <param name="canonical">The canonical set it came from, when it came from one.</param>
    /// <param name="dependencies">The participants for the set.</param>
    private sealed class GroupedSlot(string[] groups, GroupSet? canonical, IMessageDependencies dependencies)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
    }
}
