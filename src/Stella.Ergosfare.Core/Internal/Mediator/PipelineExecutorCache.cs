using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

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
    private sealed class GroupedVoidExecutorSlot(string[] groups, GroupSet? canonical, IPipelineExecutor executor)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IPipelineExecutor Executor = executor;
    }

    /// <summary>Immutable (groups, result type, executor) triple; see <see cref="ResultExecutorSlot"/>.</summary>
    private sealed class GroupedResultExecutorSlot(string[] groups, GroupSet? canonical, Type resultType, object executor)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly Type ResultType = resultType;
        public readonly object Executor = executor;
    }

    /// <summary>
    /// Slot match with the canonical fast path: a <see cref="GroupSet"/> that is the very
    /// instance the slot was built from matches on one reference check — interning makes
    /// that the steady state for callers reusing a filter. Everything else (a different
    /// or un-interned set, a plain sequence) falls to the ordinal element-wise compare.
    /// Only immutable <see cref="GroupSet"/> instances take the reference shortcut; a
    /// reused mutable list must keep being compared by content so in-place mutation is
    /// always observed.
    /// </summary>
    internal static bool SlotMatches(IEnumerable<string> groups, string[] cachedNames, GroupSet? canonical)
        => groups is GroupSet set
            ? ReferenceEquals(set, canonical) || GroupsMatch(set, cachedNames)
            : GroupsMatch(groups, cachedNames);

    /// <summary>
    /// Ordinal element-wise comparison of the caller's group sequence against a cached
    /// snapshot, allocation-free for the <see cref="GroupSet"/>, array and list shapes.
    /// Order is significant, matching the joined composite key exactly.
    /// </summary>
    internal static bool GroupsMatch(IEnumerable<string> groups, string[] cached)
    {
        switch (groups)
        {
            case GroupSet set:
            {
                var names = set.Names;

                if (names.Length != cached.Length)
                {
                    return false;
                }

                for (var i = 0; i < names.Length; i++)
                {
                    if (!string.Equals(names[i], cached[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
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
            && SlotMatches(groups, slot.Groups, slot.Canonical))
        {
            return slot.Executor;
        }

        // A canonical set contributes its immutable name array and precomputed key
        // directly — the refresh allocates nothing for it.
        var canonical = groups as GroupSet;
        var materializedGroups = canonical?.Names ?? MaterializeGroups(groups);
        var key = (messageType, canonical?.JoinedKey ?? GroupsKey(materializedGroups));

        if (!_voidExecutors.TryGetValue(key, out var executor))
        {
            executor = _voidExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateVoidExecutor(k.MessageType, state.Groups),
                (Cache: this, Groups: materializedGroups));
        }

        _groupedVoidSlotsByType[messageType] = new GroupedVoidExecutorSlot(materializedGroups, canonical, executor);

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
            && SlotMatches(groups, groupedSlot.Groups, groupedSlot.Canonical))
        {
            // Slot entries are only ever created as IPipelineExecutor<TResult> for their
            // recorded result type; see the group-less slot above.
            return Unsafe.As<IPipelineExecutor<TResult>>(groupedSlot.Executor);
        }

        var canonical = groups as GroupSet;
        var materializedGroups = canonical?.Names ?? MaterializeGroups(groups);
        var key = (messageType, typeof(TResult), canonical?.JoinedKey ?? GroupsKey(materializedGroups));

        if (!_resultExecutors.TryGetValue(key, out var executor))
        {
            executor = _resultExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateResultExecutor(k.MessageType, k.ResultType, state.Groups),
                (Cache: this, Groups: materializedGroups));
        }

        _groupedResultSlotsByType[messageType] = new GroupedResultExecutorSlot(materializedGroups, canonical, typeof(TResult), executor);

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

        // Staged plan: bespoke code for the whole interceptor-bearing pipeline. Checked
        // before the single-handler plan — generation emits at most one plan kind per
        // message, and the staged one is the more specific claim. Group-less pipelines
        // only, like every plan below.
        if (groups.Length == 0 && GeneratedDispatchRoots.FindStagedVoidPlan(messageType) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedVoidExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups,
                    StagedPlan: stagedPlan));
        }

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

        // Staged plan first, mirroring the void side.
        if (groups.Length == 0 && GeneratedDispatchRoots.FindStagedResultPlan(messageType, resultType) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedResultExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups,
                    StagedPlan: stagedPlan));
        }

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
    /// or <c>Func&lt;IServiceProvider, THandler&gt;</c> (cast back inside the closed
    /// generic), or <c>null</c> for plain roots and plans without a construction path.
    /// <paramref name="StagedPlan"/> is a staged plan carried erased (cast back to its
    /// typed base inside the closed generic), or <c>null</c> for every other root.
    /// </summary>
    private readonly record struct ExecutorState(
        IMessageDescriptor Descriptor,
        IMessageDependenciesFactory DependenciesFactory,
        IResultAdapterService? ResultAdapterService,
        string[] Groups,
        object? DirectHandlerFactory = null,
        object? StagedPlan = null);

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
                state.DirectHandlerFactory as Func<THandler>,
                state.DirectHandlerFactory as Func<IServiceProvider, THandler>);
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
                state.DirectHandlerFactory as Func<THandler>,
                state.DirectHandlerFactory as Func<IServiceProvider, THandler>);
    }

    /// <summary>
    /// Re-enters a generic context with a staged plan's message type and constructs the
    /// plan-hosting executor there — no reflection.
    /// </summary>
    private sealed class StagedVoidExecutorVisitor : IStagedVoidPlanVisitor<IPipelineExecutor, ExecutorState>
    {
        public static readonly StagedVoidExecutorVisitor Instance = new();

        public IPipelineExecutor Visit<TMessage>(ExecutorState state)
            where TMessage : notnull, IMessage
            => new StagedVoidPipelineExecutor<TMessage>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups,
                (StagedVoidPlan<TMessage>)state.StagedPlan!);
    }

    /// <summary>Result-executor counterpart of <see cref="StagedVoidExecutorVisitor"/>.</summary>
    private sealed class StagedResultExecutorVisitor : IStagedResultPlanVisitor<object, ExecutorState>
    {
        public static readonly StagedResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult>(ExecutorState state)
            where TMessage : notnull, IMessage
            => new StagedResultPipelineExecutor<TMessage, TResult>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups,
                (StagedResultPlan<TMessage, TResult>)state.StagedPlan!);
    }

    private IMessageDescriptor FindDescriptor(Type messageType)
    {
        return messageResolveStrategy.Find(messageType)
               ?? throw new NoHandlerFoundException(messageType);
    }
}
