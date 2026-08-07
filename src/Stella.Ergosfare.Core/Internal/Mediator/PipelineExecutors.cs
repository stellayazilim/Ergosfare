using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing pipeline closed over the concrete <typeparamref name="TMessage"/>.
/// Because <typeparamref name="TMessage"/> is the message's runtime type here, the mediation
/// strategy's typed seam always hits — the handler is invoked through its typed member and
/// its <see cref="ValueTask{TResult}"/> never crosses an object-typed bridge.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class ResultPipelineExecutor<TMessage, TResult>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups) : IPipelineExecutor<TResult>
    where TMessage : notnull
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new(resultAdapterService);

    // Adapter service split by shape: the concrete service can report emptiness cheaply,
    // a foreign implementation always routes through the strategy (which consults it).
    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    // Dependencies cached per executor (executors are already per message type + groups),
    // re-validated against the registry version — turns the per-dispatch factory call and
    // cache lookup into a single field read + version compare.
    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;

    public ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        // Zero-interceptor, single-handler, no-adapter dispatch: invoke the handler's typed
        // member directly and hand its ValueTask straight back — no async state machine,
        // no interface-dispatched Count checks. Mirrors the strategy's fast path exactly.
        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }

            // Unsupported handler contract: fall through so the strategy raises its
            // canonical NotSupportedException.
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            // Create runs the registry-version invalidation and rebuilds; races are benign —
            // both writers publish equivalent, idempotent state.
            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        // Foreign factory implementations keep the original per-dispatch behavior.
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}

#pragma warning restore CS8714

/// <summary>
/// Void pipeline closed over the concrete <typeparamref name="TMessage"/>; see
/// <see cref="ResultPipelineExecutor{TMessage, TResult}"/>.
/// </summary>
internal sealed class VoidPipelineExecutor<TMessage>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups) : IPipelineExecutor
    where TMessage : IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;

    public ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle((TMessage)message, context);
                    return ValueTask.CompletedTask;
            }
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}

/// <summary>
/// Void pipeline closed over both the message and its compile-time-known sole handler —
/// the executor a generated void plan constructs. The fast path resolves the handler
/// reference exactly like <see cref="VoidPipelineExecutor{TMessage}"/> but invokes it
/// through the closed <typeparamref name="THandler"/> type, so the call devirtualizes
/// (and inlines for sealed handlers) instead of walking the contract pattern match. The
/// plan is advisory: the same registry-version-guarded dependency cache re-validates the
/// pipeline, and any mismatch — interceptors registered at runtime, a differently-typed
/// handler instance, configured adapters — falls back to the runtime dispatch shape,
/// preserving semantics exactly.
/// </summary>
internal sealed class GeneratedVoidPipelineExecutor<TMessage, THandler>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups,
    Func<THandler>? directHandlerFactory = null) : IPipelineExecutor
    where TMessage : notnull, IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    // Compile-time construction path for the planned handler; discarded up front for
    // disposable handlers — the container tracks transient disposables in the resolving
    // scope, direct construction would not.
    private readonly Func<THandler>? _directHandlerFactory =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler))
            ? null
            : directHandlerFactory;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;

    // Re-validated with the dependency cache: true only while the registry's sole handler
    // is the planned type, instances are not memoized, and the handler's effective DI
    // registration is the module's own plain transient one — the exact conditions under
    // which GetRequiredService is observably nothing but a constructor call.
    private bool _useDirectConstruction;

    public ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
        {
            // The handler-type re-check pins the racy flag to the reference actually in
            // hand: a version transition observed halfway can only route back through the
            // container, never construct a type the registry no longer plans.
            IHandler handler = _useDirectConstruction && handlerReference.HandlerType == typeof(THandler)
                ? _directHandlerFactory!()
                : handlerReference.Resolve(serviceProvider);

            // The compile-time plan's handler type: a devirtualized call, no pattern
            // match. A runtime re-registration can put a differently-typed handler here;
            // the contract switch below then dispatches it exactly as the runtime
            // executor would.
            if (handler is THandler planned)
            {
                return planned.HandleAsync((TMessage)message, context);
            }

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle((TMessage)message, context);
                    return ValueTask.CompletedTask;
            }
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            var fastDependencies = dependencies as MessageDependencies;
            _cachedFastDependencies = fastDependencies;
            _cachedDependencies = dependencies;
            _useDirectConstruction = _directHandlerFactory is not null
                && fastDependencies is { MemoizedInstances: false, FastSingleHandler.HandlerType: var plannedType }
                && plannedType == typeof(THandler)
                && typedFactory.IsPlainTransientRegistration(typeof(THandler));
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}

/// <summary>
/// Result-producing counterpart of
/// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>: closed over the
/// message, its result and its compile-time-known sole async handler, so the handler call
/// devirtualizes instead of walking the contract pattern match. The same advisory-plan
/// contract applies — the registry-version-guarded dependency cache re-validates the
/// pipeline and any mismatch falls back to the runtime dispatch shape.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups,
    Func<THandler>? directHandlerFactory = null) : IPipelineExecutor<TResult>
    where TMessage : notnull, IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    private readonly Func<THandler>? _directHandlerFactory =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler))
            ? null
            : directHandlerFactory;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;
    private bool _useDirectConstruction;

    public ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
        {
            IHandler handler = _useDirectConstruction && handlerReference.HandlerType == typeof(THandler)
                ? _directHandlerFactory!()
                : handlerReference.Resolve(serviceProvider);

            if (handler is THandler planned)
            {
                return planned.HandleAsync((TMessage)message, context);
            }

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            var fastDependencies = dependencies as MessageDependencies;
            _cachedFastDependencies = fastDependencies;
            _cachedDependencies = dependencies;
            _useDirectConstruction = _directHandlerFactory is not null
                && fastDependencies is { MemoizedInstances: false, FastSingleHandler.HandlerType: var plannedType }
                && plannedType == typeof(THandler)
                && typedFactory.IsPlainTransientRegistration(typeof(THandler));
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}
#pragma warning restore CS8714

/// <summary>
/// Process-wide cache of pipeline executors, one per (message runtime type, result type,
/// group set). Executor construction closes the generic executor over the message's runtime
/// type — one <see cref="Type.MakeGenericType"/> per message type, consistent with the
/// pipeline plan premise that all dispatch-shape work happens once per message type.
/// </summary>
internal sealed class PipelineExecutorCache(
    IMessageDependenciesFactory dependenciesFactory,
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService = null)
{
    internal static readonly string[] EmptyGroups = [];

    private readonly ConcurrentDictionary<(Type MessageType, string GroupsKey), IPipelineExecutor> _voidExecutors = new();
    private readonly ConcurrentDictionary<(Type MessageType, Type ResultType, string GroupsKey), object> _resultExecutors = new();

    // Group-less dispatch (the overwhelmingly common case) is keyed by message type alone:
    // no group materialization, no composite-key hashing on the hot path.
    private readonly ConcurrentDictionary<Type, IPipelineExecutor> _voidExecutorsByType = new();
    private readonly ConcurrentDictionary<(Type MessageType, Type ResultType), object> _resultExecutorsByType = new();

    // Last-used result executor per message type: one Type-keyed lookup plus a reference
    // equality check replaces the composite (message, result) key's tuple hashing on the
    // result hot path — a message type practically has a single result type. A slot miss
    // falls back to the composite store above, which stays authoritative so executor
    // identity (and its dependency cache) is preserved even when result types alternate.
    private readonly ConcurrentDictionary<Type, ResultExecutorSlot> _resultSlotsByType = new();

    /// <summary>
    /// Immutable (result type, executor) pair — immutability makes the racy slot refresh
    /// safe: a reader that observes the reference sees both fields.
    /// </summary>
    private sealed class ResultExecutorSlot(Type resultType, object executor)
    {
        public readonly Type ResultType = resultType;
        public readonly object Executor = executor;
    }

    // Last-used grouped executor per message type: an ordinal element-wise compare of the
    // caller's group sequence against the slot's snapshot replaces the per-dispatch group
    // materialization and joined-string key of the composite stores — the overwhelmingly
    // common grouped caller dispatches one message type with one stable group set. A slot
    // miss falls back to the composite store, which stays authoritative so executor
    // identity (and its dependency cache) is preserved when group sets alternate.
    private readonly ConcurrentDictionary<Type, GroupedVoidExecutorSlot> _groupedVoidSlotsByType = new();
    private readonly ConcurrentDictionary<Type, GroupedResultExecutorSlot> _groupedResultSlotsByType = new();

    /// <summary>Immutable (groups, executor) pair; see <see cref="ResultExecutorSlot"/> for the refresh contract.</summary>
    private sealed class GroupedVoidExecutorSlot(string[] groups, IPipelineExecutor executor)
    {
        public readonly string[] Groups = groups;
        public readonly IPipelineExecutor Executor = executor;
    }

    /// <summary>Immutable (groups, result type, executor) triple; see <see cref="ResultExecutorSlot"/>.</summary>
    private sealed class GroupedResultExecutorSlot(string[] groups, Type resultType, object executor)
    {
        public readonly string[] Groups = groups;
        public readonly Type ResultType = resultType;
        public readonly object Executor = executor;
    }

    /// <summary>
    /// Ordinal element-wise comparison of the caller's group sequence against a cached
    /// snapshot, allocation-free for the array and list shapes settings expose. Order is
    /// significant, matching the joined composite key exactly.
    /// </summary>
    internal static bool GroupsMatch(IEnumerable<string> groups, string[] cached)
    {
        switch (groups)
        {
            case string[] array:
            {
                if (array.Length != cached.Length)
                {
                    return false;
                }

                for (var i = 0; i < array.Length; i++)
                {
                    if (!string.Equals(array[i], cached[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
            case List<string> list:
            {
                if (list.Count != cached.Length)
                {
                    return false;
                }

                for (var i = 0; i < cached.Length; i++)
                {
                    if (!string.Equals(list[i], cached[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
            default:
            {
                var index = 0;

                foreach (var group in groups)
                {
                    if (index >= cached.Length || !string.Equals(group, cached[index], StringComparison.Ordinal))
                    {
                        return false;
                    }

                    index++;
                }

                return index == cached.Length;
            }
        }
    }

    /// <summary>
    /// Group-less void executor lookup for a compile-time-known message type: a
    /// static-generic slot replaces the dictionary lookup with a field read and a cache
    /// identity check. The slot is keyed by this cache instance, so containers stay
    /// isolated — a foreign cache's executor is never served, and the authoritative
    /// per-type dictionary below preserves executor identity across slot refreshes.
    /// Callers must guard with <c>message.GetType() == typeof(TMessage)</c>; a base-typed
    /// generic call must keep resolving by the runtime type.
    /// </summary>
    public IPipelineExecutor GetVoidExecutor<TMessage>() where TMessage : IMessage
    {
        var slot = VoidExecutorHolder<TMessage>.Slot;

        if (slot is not null && ReferenceEquals(slot.Cache, this))
        {
            return slot.Executor;
        }

        var executor = GetVoidExecutor(typeof(TMessage));
        VoidExecutorHolder<TMessage>.Slot = new VoidExecutorSlot(this, executor);

        return executor;
    }

    /// <summary>
    /// Immutable (cache, executor) pair — immutability makes the racy slot refresh safe:
    /// a reader that observes the reference sees both fields.
    /// </summary>
    private sealed class VoidExecutorSlot(PipelineExecutorCache cache, IPipelineExecutor executor)
    {
        public readonly PipelineExecutorCache Cache = cache;
        public readonly IPipelineExecutor Executor = executor;
    }

    /// <summary>
    /// Per-message-type slot for the last cache instance that served a typed void lookup.
    /// Multiple containers alternating over one message type refresh the slot each time —
    /// correct either way, and the single-container case (every production process) reads
    /// a stable field forever. The strong reference deliberately roots the last-serving
    /// cache (and through it that container's executor graph) past container disposal:
    /// at most one graph per message type, which in a single-container process is the
    /// live one anyway — a WeakReference would tax every hot-path read instead.
    /// </summary>
    private static class VoidExecutorHolder<TMessage> where TMessage : IMessage
    {
        public static VoidExecutorSlot? Slot;
    }

    public IPipelineExecutor GetVoidExecutor(Type messageType, IEnumerable<string>? groups = null)
    {
        if (groups is null)
        {
            if (_voidExecutorsByType.TryGetValue(messageType, out var fast))
            {
                return fast;
            }

            return _voidExecutorsByType.GetOrAdd(messageType,
                static (t, cache) => cache.CreateVoidExecutor(t, EmptyGroups), this);
        }

        if (_groupedVoidSlotsByType.TryGetValue(messageType, out var slot)
            && GroupsMatch(groups, slot.Groups))
        {
            return slot.Executor;
        }

        var materializedGroups = MaterializeGroups(groups);
        var key = (messageType, GroupsKey(materializedGroups));

        if (!_voidExecutors.TryGetValue(key, out var executor))
        {
            executor = _voidExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateVoidExecutor(k.MessageType, state.Groups),
                (Cache: this, Groups: materializedGroups));
        }

        _groupedVoidSlotsByType[messageType] = new GroupedVoidExecutorSlot(materializedGroups, executor);

        return executor;
    }

    public IPipelineExecutor<TResult> GetExecutor<TResult>(Type messageType, IEnumerable<string>? groups = null)
    {
        if (groups is null)
        {
            if (_resultSlotsByType.TryGetValue(messageType, out var slot)
                && ReferenceEquals(slot.ResultType, typeof(TResult)))
            {
                // Slot entries are only ever created as IPipelineExecutor<TResult> for
                // their recorded result type, so the interface cast can skip the runtime
                // covariance check — a measurable cost on the hot path.
                return Unsafe.As<IPipelineExecutor<TResult>>(slot.Executor);
            }

            return GetExecutorSlow<TResult>(messageType);
        }

        if (_groupedResultSlotsByType.TryGetValue(messageType, out var groupedSlot)
            && ReferenceEquals(groupedSlot.ResultType, typeof(TResult))
            && GroupsMatch(groups, groupedSlot.Groups))
        {
            // Slot entries are only ever created as IPipelineExecutor<TResult> for their
            // recorded result type; see the group-less slot above.
            return Unsafe.As<IPipelineExecutor<TResult>>(groupedSlot.Executor);
        }

        var materializedGroups = MaterializeGroups(groups);
        var key = (messageType, typeof(TResult), GroupsKey(materializedGroups));

        if (!_resultExecutors.TryGetValue(key, out var executor))
        {
            executor = _resultExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateResultExecutor(k.MessageType, k.ResultType, state.Groups),
                (Cache: this, Groups: materializedGroups));
        }

        _groupedResultSlotsByType[messageType] = new GroupedResultExecutorSlot(materializedGroups, typeof(TResult), executor);

        return (IPipelineExecutor<TResult>)executor;
    }

    /// <summary>
    /// Slot miss for the group-less result dispatch: resolve (or create) the executor in
    /// the authoritative composite store, then refresh the message type's last-used slot.
    /// Rare by construction — first dispatch per message type, or alternating result
    /// types on one message type.
    /// </summary>
    private IPipelineExecutor<TResult> GetExecutorSlow<TResult>(Type messageType)
    {
        var executor = (IPipelineExecutor<TResult>)_resultExecutorsByType.GetOrAdd(
            (messageType, typeof(TResult)),
            static (k, cache) => cache.CreateResultExecutor(k.MessageType, k.ResultType, EmptyGroups), this);

        _resultSlotsByType[messageType] = new ResultExecutorSlot(typeof(TResult), executor);

        return executor;
    }

    private static string GroupsKey(string[] groups)
        => groups.Length == 0 ? string.Empty : string.Join('\x1f', groups);

    /// <summary>
    /// Snapshots the caller's group sequence exactly once: the same array both builds the
    /// cache key and flows into the executor, so a lazy or unstable enumerable can never
    /// produce a key that disagrees with the groups the cached executor was built with.
    /// </summary>
    private static string[] MaterializeGroups(IEnumerable<string>? groups)
        => groups is null ? EmptyGroups : [.. groups];

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The executor generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for open generics and runtime-only registrations.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "Executor types are constructed from typeof expressions below; their constructors are rooted.")]
    private IPipelineExecutor CreateVoidExecutor(Type messageType, string[] groups)
    {
        var descriptor = FindDescriptor(messageType);

        // Generated void plan: closed over (message, handler) at compile time, so the
        // fast path calls the handler devirtualized. Group-less pipelines only — a
        // grouped pipeline may exclude the planned handler, and the plain executor
        // serves that shape without the plan's permanently-missing fast check.
        if (groups.Length == 0 && GeneratedDispatchRoots.FindVoidPlan(messageType) is { } plan)
        {
            return plan.Accept(
                GeneratedVoidExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups,
                    plan.DirectHandlerFactory));
        }

        // Generated dispatch roots close the executor generic at compile time; the
        // reflective path below only serves types without a root.
        if (GeneratedDispatchRoots.FindMessage(messageType) is { } root)
        {
            return root.Accept(
                VoidExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups));
        }

        var executorType = typeof(VoidPipelineExecutor<>).MakeGenericType(messageType);

        return (IPipelineExecutor)Activator.CreateInstance(executorType, descriptor, dependenciesFactory, resultAdapterService, groups)!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The executor generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for open generics and runtime-only registrations.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "Executor types are constructed from typeof expressions below; their constructors are rooted.")]
    private object CreateResultExecutor(Type messageType, Type resultType, string[] groups)
    {
        var descriptor = FindDescriptor(messageType);

        // Generated result plan: closed over (message, result, handler) at compile time,
        // so the fast path calls the handler devirtualized. Group-less pipelines only,
        // mirroring the void plan above.
        if (groups.Length == 0 && GeneratedDispatchRoots.FindResultPlan(messageType, resultType) is { } plan)
        {
            return plan.Accept(
                GeneratedResultExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups,
                    plan.DirectHandlerFactory));
        }

        if (GeneratedDispatchRoots.FindResult(messageType, resultType) is { } root)
        {
            return root.Accept(
                ResultExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups));
        }

        var executorType = typeof(ResultPipelineExecutor<,>).MakeGenericType(messageType, resultType);

        return Activator.CreateInstance(executorType, descriptor, dependenciesFactory, resultAdapterService, groups)!;
    }

    /// <summary>
    /// Constructor arguments carried into the generic re-entry of a dispatch root.
    /// <paramref name="DirectHandlerFactory"/> is a plan's erased <c>Func&lt;THandler&gt;</c>
    /// (cast back inside the closed generic), or <c>null</c> for plain roots and plans
    /// without a construction path.
    /// </summary>
    private readonly record struct ExecutorState(
        IMessageDescriptor Descriptor,
        IMessageDependenciesFactory DependenciesFactory,
        IResultAdapterService? ResultAdapterService,
        string[] Groups,
        object? DirectHandlerFactory = null);

    /// <summary>
    /// Re-enters a generic context with a root's message type and constructs the closed
    /// void executor there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class VoidExecutorVisitor : IMessageRootVisitor<IPipelineExecutor, ExecutorState>
    {
        public static readonly VoidExecutorVisitor Instance = new();

        public IPipelineExecutor Visit<TMessage>(ExecutorState state) where TMessage : IMessage
            => new VoidPipelineExecutor<TMessage>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups);
    }

    /// <summary>Result-executor counterpart of <see cref="VoidExecutorVisitor"/>.</summary>
    private sealed class ResultExecutorVisitor : IMessageResultRootVisitor<object, ExecutorState>
    {
        public static readonly ResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult>(ExecutorState state) where TMessage : IMessage
            => new ResultPipelineExecutor<TMessage, TResult>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups);
    }

    /// <summary>
    /// Re-enters a generic context with a generated void plan's (message, handler) pair
    /// and constructs the plan-closed executor there — no reflection, and the handler
    /// call devirtualizes inside the closed generic.
    /// </summary>
    private sealed class GeneratedVoidExecutorVisitor : IVoidPlanRootVisitor<IPipelineExecutor, ExecutorState>
    {
        public static readonly GeneratedVoidExecutorVisitor Instance = new();

        public IPipelineExecutor Visit<TMessage, THandler>(ExecutorState state)
            where TMessage : notnull, IMessage
            where THandler : class, IAsyncHandler<TMessage>
            => new GeneratedVoidPipelineExecutor<TMessage, THandler>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups,
                state.DirectHandlerFactory as Func<THandler>);
    }

    /// <summary>
    /// Re-enters a generic context with a generated result plan's (message, result,
    /// handler) triple and constructs the plan-closed executor there; the result-producing
    /// counterpart of <see cref="GeneratedVoidExecutorVisitor"/>.
    /// </summary>
    private sealed class GeneratedResultExecutorVisitor : IResultPlanRootVisitor<object, ExecutorState>
    {
        public static readonly GeneratedResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult, THandler>(ExecutorState state)
            where TMessage : notnull, IMessage
            where THandler : class, IAsyncHandler<TMessage, TResult>
            => new GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups,
                state.DirectHandlerFactory as Func<THandler>);
    }

    private IMessageDescriptor FindDescriptor(Type messageType)
    {
        return messageResolveStrategy.Find(messageType)
               ?? throw new NoHandlerFoundException(messageType);
    }
}
