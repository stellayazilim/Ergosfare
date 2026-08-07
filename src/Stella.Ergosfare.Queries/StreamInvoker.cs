using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Contexts;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;

/// <summary>
/// Streams a query through a pipeline closed over the query's concrete type, so the stream
/// handler is always invoked through its typed member — interface-erased streaming
/// (<c>StreamAsync(IStreamQuery&lt;T&gt;)</c>) resolves the invoker from the query's runtime
/// type. Invokers are closed once per (query type, result type) and cached; the per-call
/// cancellation token flows into a fresh strategy instance, as before.
/// </summary>
internal interface IQueryStreamInvoker<out TResult>
{
    IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy);

    /// <summary>
    /// Engine-backed streaming: the concrete dispatch machinery is known by construction,
    /// so the stream runs against the invoker-cached, registry-version-guarded pipeline
    /// plan — no <c>MediateOptions</c>, no per-call descriptor lookup, no scope-resolved
    /// mediator. Grouped streams resolve the same group-filtered dependencies the Mediate
    /// path would build, from a last-used group-set slot.
    /// </summary>
    IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy);
}

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
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy)
    {
        var options = new MediateOptions<TQuery, IAsyncEnumerable<TResult>>
        {
            MessageMediationStrategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(SharedEmptyAdapters, cancellationToken),
            MessageResolveStrategy = resolveStrategy,
            CancellationToken = cancellationToken,
            Items = settings?.Items,
            Groups = settings?.Filters.Groups ?? EmptyGroups,
        };

        return mediator.Mediate((TQuery)query, options);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy)
    {
        var groups = settings?.Filters.Groups;
        var dependencies = groups is null or List<string> { Count: 0 } or string[] { Length: 0 }
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
            var cached = _cachedDependencies;

            if (cached is not null
                && ReferenceEquals(_cachedFactory, typedFactory)
                && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = BuildPlan(typedFactory, resolveStrategy, EmptyGroups);
            _cachedDependencies = dependencies;
            _cachedFactory = typedFactory;
            _cachedVersion = typedFactory.CurrentRegistryVersion;
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
        IMessageDependencies dependencies,
        int version)
    {
        public readonly MessageDependenciesFactory Factory = factory;
        public readonly string[] Groups = groups;
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
            var slot = _cachedGroupedPlan;

            if (slot is not null
                && ReferenceEquals(slot.Factory, typedFactory)
                && slot.Version == typedFactory.CurrentRegistryVersion
                && Core.Internal.Mediator.PipelineExecutorCache.GroupsMatch(groups, slot.Groups))
            {
                return slot.Dependencies;
            }

            string[] materialized = [.. groups];
            var dependencies = BuildPlan(typedFactory, resolveStrategy, materialized);
            _cachedGroupedPlan = new GroupedPlanSlot(
                typedFactory, materialized, dependencies, typedFactory.CurrentRegistryVersion);
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

/// <summary>
/// Process-wide cache of <see cref="IQueryStreamInvoker{TResult}"/> instances, one per
/// (query runtime type, result type) — one <see cref="Type.MakeGenericType"/> per pair.
/// </summary>
internal static class QueryStreamInvokerCache
{
    private static readonly ConcurrentDictionary<(Type QueryType, Type ResultType), object> Invokers = new();

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The invoker generic is closed over a live query's runtime type; the query roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated query types; this reflective path is the " +
                        "JIT fallback for runtime-only registrations.")]
    public static IQueryStreamInvoker<TResult> Get<TResult>(Type queryType)
    {
        var key = (queryType, typeof(TResult));

        if (!Invokers.TryGetValue(key, out var invoker))
        {
            // Generated dispatch roots close the invoker generic at compile time; the
            // reflective path below only serves query types without a root.
            invoker = GeneratedDispatchRoots.FindStream(queryType, typeof(TResult)) is { } root
                ? Invokers.GetOrAdd(key, root.Accept(InvokerVisitor.Instance, state: false))
                : Invokers.GetOrAdd(key,
                    static k => Activator.CreateInstance(typeof(QueryStreamInvoker<,>).MakeGenericType(k.QueryType, k.ResultType))!);
        }

        return (IQueryStreamInvoker<TResult>)invoker;
    }

    /// <summary>
    /// Re-enters a generic context with a root's (query, result) pair and constructs the
    /// closed stream invoker there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class InvokerVisitor : IMessageResultRootVisitor<object, bool>
    {
        public static readonly InvokerVisitor Instance = new();

        public object Visit<TMessage, TResult>(bool state) where TMessage : IMessage
            => new QueryStreamInvoker<TMessage, TResult>();
    }
}
