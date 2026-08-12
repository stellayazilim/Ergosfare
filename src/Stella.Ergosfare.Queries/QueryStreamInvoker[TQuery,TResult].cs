using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;

internal sealed class QueryStreamInvoker<TQuery, TResult> : IQueryStreamInvoker<TResult>
    where TQuery : notnull
{
    // One copy per closed query type is deliberate — the invoker itself is per-query-type.
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string[] EmptyGroups = [];

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        IEnumerable<string>? groupsOverride = null)
    {
        var groups = groupsOverride ?? settings?.Filters.Groups;
        var dependencies = groups is null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }
            ? GetPlan(engine.DependenciesFactory)
            : GetGroupedPlan(engine.DependenciesFactory, groups);

        // Exactly the construction Mediate performs for streams: a fresh, unpooled
        // context — enumeration happens after this call returns, so its completion is
        // not observable here and the context cannot be pooled.
        var context = new ErgosfareContext(settings?.Items, cancellationToken);
        var strategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(cancellationToken);

        return strategy.Mediate((TQuery)query, dependencies, context, serviceProvider);
    }

    /// <summary>
    /// The group-less pipeline plan for <typeparamref name="TQuery"/>, cached on the
    /// invoker — the broadcast invoker's pattern. A composition is settled before the
    /// container is built, so the plan never goes stale. The invoker is process-wide while factories are per-container, so the
    /// factory reference is part of the cache key and one container's plan is never
    /// served to another.
    /// </summary>
    private IMessageDependencies? _cachedDependencies;
    private MessageDependenciesFactory? _cachedFactory;

    private IMessageDependencies GetPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && ReferenceEquals(_cachedFactory, typedFactory))
            {
                return cached;
            }

            var dependencies = BuildPlan(typedFactory, EmptyGroups);
            _cachedDependencies = dependencies;
            _cachedFactory = typedFactory;
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, EmptyGroups);
    }

    /// <summary>
    /// Grouped counterpart of <see cref="GetPlan"/>: a single last-used
    /// (factory, group set) slot with an ordinal element-wise compare, snapshotting the
    /// caller's group sequence so later mutation of a reused settings instance reads as a
    /// different group set. A slot miss only rebuilds through the factory's process-wide
    /// dependency cache.
    /// </summary>
    private GroupedPlanSlot? _cachedGroupedPlan;

    private sealed class GroupedPlanSlot(
        MessageDependenciesFactory factory,
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies)
    {
        public readonly MessageDependenciesFactory Factory = factory;
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
    }

    private IMessageDependencies GetGroupedPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory,
        IEnumerable<string> groups)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var slot = _cachedGroupedPlan;

            // Deliberate: groups is matched allocation-free first and only materialized on a slot miss.
            // ReSharper disable once PossibleMultipleEnumeration
            if (slot is not null
                && ReferenceEquals(slot.Factory, typedFactory)
                && Core.Internal.Mediator.PipelineExecutorCache.SlotMatches(groups, slot.Groups, slot.Canonical))
            {
                return slot.Dependencies;
            }

            var canonical = groups as GroupSet;
            // ReSharper disable once PossibleMultipleEnumeration
            string[] materialized = [.. groups];
            var dependencies = BuildPlan(typedFactory, materialized);
            _cachedGroupedPlan = new GroupedPlanSlot(
                typedFactory, materialized, canonical, dependencies);
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, [.. groups]);
    }

    private static IMessageDependencies BuildPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory factory,
        string[] groups)
        // A query with no composition is a failed dispatch, not an empty stream:
        // Create throws NoHandlerFoundException exactly as the descriptor lookup did.
        => factory.Create(typeof(TQuery), groups);
}
