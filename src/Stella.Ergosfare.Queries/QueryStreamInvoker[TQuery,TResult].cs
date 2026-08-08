using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Contexts;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;

internal sealed class QueryStreamInvoker<TQuery, TResult> : IQueryStreamInvoker<TResult>
    where TQuery : notnull
{
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// The stream path has always run against a fresh, empty adapter service (the facade
    /// carries none); a single shared empty instance preserves that behavior without the
    /// per-call allocation. Never exposed, so it cannot be mutated.
    /// </summary>
    private static readonly ResultAdapterService SharedEmptyAdapters = new();

    public IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IEnumerable<string>? groupsOverride = null)
    {
        var options = new MediateOptions<TQuery, IAsyncEnumerable<TResult>>
        {
            MessageMediationStrategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(SharedEmptyAdapters, cancellationToken),
            MessageResolveStrategy = resolveStrategy,
            CancellationToken = cancellationToken,
            Items = settings?.Items,
            Groups = groupsOverride ?? settings?.Filters.Groups ?? EmptyGroups,
        };

        return mediator.Mediate((TQuery)query, options);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IEnumerable<string>? groupsOverride = null)
    {
        var groups = groupsOverride ?? settings?.Filters.Groups;
        var dependencies = groups is null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }
            ? GetPlan(engine.DependenciesFactory, resolveStrategy)
            : GetGroupedPlan(engine.DependenciesFactory, resolveStrategy, groups);

        // Exactly the construction Mediate performs for streams: a fresh, unpooled
        // context — enumeration happens after this call returns, so its completion is
        // not observable here and the context cannot be pooled.
        var context = new ErgosfareExecutionContext(settings?.Items, cancellationToken);
        var strategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(SharedEmptyAdapters, cancellationToken);

        return strategy.Mediate((TQuery)query, dependencies, context, serviceProvider);
    }

    /// <summary>
    /// The group-less pipeline plan for <typeparamref name="TQuery"/>, cached on the
    /// invoker and re-validated against the registry version — the broadcast invoker's
    /// pattern. The invoker is process-wide while factories are per-container, so the
    /// factory reference is part of the cache key and one container's plan is never
    /// served to another.
    /// </summary>
    private IMessageDependencies? _cachedDependencies;
    private MessageDependenciesFactory? _cachedFactory;
    private int _cachedVersion = int.MinValue;

    private IMessageDependencies GetPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            // Read before the build: a registration completing mid-build must land as a
            // version mismatch on the next dispatch, never as a fresh stamp on stale deps.
            var registryVersion = typedFactory.CurrentRegistryVersion;
            var cached = _cachedDependencies;

            if (cached is not null
                && ReferenceEquals(_cachedFactory, typedFactory)
                && _cachedVersion == registryVersion)
            {
                return cached;
            }

            var dependencies = BuildPlan(typedFactory, resolveStrategy, EmptyGroups);
            _cachedDependencies = dependencies;
            _cachedFactory = typedFactory;
            _cachedVersion = registryVersion;
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, resolveStrategy, EmptyGroups);
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
        IMessageDependencies dependencies,
        int version)
    {
        public readonly MessageDependenciesFactory Factory = factory;
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
        public readonly int Version = version;
    }

    private IMessageDependencies GetGroupedPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IEnumerable<string> groups)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            // Read before the build: a registration completing mid-build must land as a
            // version mismatch on the next dispatch, never as a fresh stamp on stale deps.
            var registryVersion = typedFactory.CurrentRegistryVersion;
            var slot = _cachedGroupedPlan;

            if (slot is not null
                && ReferenceEquals(slot.Factory, typedFactory)
                && slot.Version == registryVersion
                && Core.Internal.Mediator.PipelineExecutorCache.SlotMatches(groups, slot.Groups, slot.Canonical))
            {
                return slot.Dependencies;
            }

            var canonical = groups as GroupSet;
            string[] materialized = [.. groups];
            var dependencies = BuildPlan(typedFactory, resolveStrategy, materialized);
            _cachedGroupedPlan = new GroupedPlanSlot(
                typedFactory, materialized, canonical, dependencies, registryVersion);
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, resolveStrategy, [.. groups]);
    }

    private static IMessageDependencies BuildPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory factory,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        string[] groups)
    {
        // Mirrors MessageMediator.Mediate's descriptor handling for the options the
        // original path used: RegisterPlainMessagesOnSpot was never set for streams, so
        // an unregistered query type throws NoHandlerFoundException here as it did there.
        var descriptor = resolveStrategy.Find(typeof(TQuery))
                         ?? throw new NoHandlerFoundException(typeof(TQuery));

        return factory.Create(typeof(TQuery), descriptor, groups);
    }
}
