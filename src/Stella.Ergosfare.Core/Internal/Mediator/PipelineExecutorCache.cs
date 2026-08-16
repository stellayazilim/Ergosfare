using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's table of message pipelines, keyed by message type and — for the
/// result-producing ones — result type. The sending counterpart of the broadcast and stream
/// tables, and the same shape as both.
/// </summary>
/// <remarks>
/// <para>
/// The group filter is deliberately absent from every key here. It used to be part of an
/// executor's identity, which cost two extra dictionaries (one per shape, keyed by a joined
/// group string) and made a plan a construction-time decision — a plan could only be given
/// to an executor that had been built for the unfiltered pipeline. The filter is a dispatch
/// argument now: one executor per message type serves every filter and chooses its
/// composition per call, exactly as the publishing table's dispatches always have.
/// </para>
/// <para>
/// What is left is the lookup itself: one dictionary per shape, plus the last-used result
/// slot and the static-generic holder that skip even that.
/// </para>
/// </remarks>
internal sealed class PipelineExecutorCache(IMessageDependenciesFactory dependenciesFactory)
{
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

    /// <summary>
    /// Void executor lookup for a compile-time-known message type: a static-generic slot
    /// replaces the dictionary lookup with a field read and a table identity check. The
    /// slot is keyed by this table instance, so containers stay isolated — a foreign
    /// table's executor is never served, and the authoritative per-type dictionary below
    /// preserves executor identity across slot refreshes. Callers must guard with
    /// <c>message.GetType() == typeof(TMessage)</c>; a base-typed generic call must keep
    /// resolving by the runtime type.
    /// </summary>
    /// <remarks>
    /// Group-filtered dispatches reach this too, now that the filter is not part of an
    /// executor's identity — the slot answers for every filter because the executor does.
    /// </remarks>
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
    // The type parameter is the cache key: one static slot per closed message type.
    // ReSharper disable once UnusedTypeParameter
    private static class VoidExecutorHolder<TMessage> where TMessage : IMessage
    {
        // ReSharper disable once StaticMemberInGenericType
        public static VoidExecutorSlot? Slot;
    }

    public IPipelineExecutor GetVoidExecutor(Type messageType)
        => _voidExecutorsByType.TryGetValue(messageType, out var executor)
            ? executor
            : _voidExecutorsByType.GetOrAdd(messageType,
                static (t, cache) => cache.CreateVoidExecutor(t), this);

    public IPipelineExecutor<TResult> GetExecutor<TResult>(Type messageType)
    {
        if (_resultSlotsByType.TryGetValue(messageType, out var slot)
            && ReferenceEquals(slot.ResultType, typeof(TResult)))
        {
            // Slot entries are only ever created as IPipelineExecutor<TResult> for their
            // recorded result type, so the interface cast can skip the runtime covariance
            // check — a measurable cost on the hot path.
            return Unsafe.As<IPipelineExecutor<TResult>>(slot.Executor);
        }

        return GetExecutorSlow<TResult>(messageType);
    }

    /// <summary>
    /// Slot miss for the result dispatch: resolve (or create) the executor in the
    /// authoritative composite store, then refresh the message type's last-used slot.
    /// Rare by construction — first dispatch per message type, or alternating result
    /// types on one message type.
    /// </summary>
    private IPipelineExecutor<TResult> GetExecutorSlow<TResult>(Type messageType)
    {
        var executor = (IPipelineExecutor<TResult>)_resultExecutorsByType.GetOrAdd(
            (messageType, typeof(TResult)),
            static (k, cache) => cache.CreateResultExecutor(k.MessageType, k.ResultType), this);

        _resultSlotsByType[messageType] = new ResultExecutorSlot(typeof(TResult), executor);

        return executor;
    }

    /// <summary>
    /// The result counterpart of <see cref="GetVoidExecutor{TMessage}"/>: both types are
    /// compile-time constants, so the executor comes from a static generic field instead of
    /// a tuple hash over two <see cref="Type"/> objects and a slot refresh. Callers must
    /// guard with <c>message.GetType() == typeof(TMessage)</c> — a base-typed generic call
    /// has to keep resolving by the runtime type.
    /// </summary>
    /// <remarks>
    /// The container guard is not optional. A static generic field is process-wide and an
    /// executor belongs to one container; without the check, two containers over the same
    /// closed pair — which every test class creates — would read each other's pipelines.
    /// The void lane learned this first and this is the same guard.
    /// </remarks>
    public IPipelineExecutor<TResult> GetExecutor<TMessage, TResult>()
        where TMessage : IMessage
    {
        var slot = ResultExecutorHolder<TMessage, TResult>.Slot;

        if (slot is not null && ReferenceEquals(slot.Cache, this))
        {
            return slot.Executor;
        }

        return GetTypedExecutorSlow<TMessage, TResult>();
    }

    /// <summary>
    /// Slot miss for the typed result dispatch. The composite store stays authoritative, so
    /// a message dispatched both ways shares one executor and one dependency cache; what
    /// differs is how the executor is built when it has to be built.
    /// </summary>
    private IPipelineExecutor<TResult> GetTypedExecutorSlow<TMessage, TResult>()
        where TMessage : IMessage
    {
        var executor = (IPipelineExecutor<TResult>)_resultExecutorsByType.GetOrAdd(
            (typeof(TMessage), typeof(TResult)),
            _ => CreateResultExecutor<TMessage, TResult>());

        // Both slots, so a later untyped dispatch of the same message reads the same
        // executor from its own fast path rather than rebuilding the lookup.
        _resultSlotsByType[typeof(TMessage)] = new ResultExecutorSlot(typeof(TResult), executor);
        ResultExecutorHolder<TMessage, TResult>.Slot = new TypedResultExecutorSlot<TResult>(this, executor);

        return executor;
    }

    /// <summary>
    /// Builds a result executor from type arguments the compiler already resolved. The plan
    /// arms are the untyped path's, because a plan carries its own closed generics and needs
    /// no closing; the last arm is where the two differ — the untyped one asks the root table
    /// and, for a message with no root, closes <c>FrozenResultDispatch&lt;,&gt;</c> with
    /// <see cref="Type.MakeGenericType"/>. Here the closed type is what the caller named, so
    /// the construction is ordinary code the compiler emitted: no root lookup, no reflection,
    /// and an answer Native AOT can give for a message the generator never saw.
    /// </summary>
    private object CreateResultExecutor<TMessage, TResult>()
        where TMessage : IMessage
    {
        if (GeneratedDispatchRoots.FindStagedResultPlan(typeof(TMessage), typeof(TResult)) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedResultExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, StagedPlan: stagedPlan));
        }

        if (GeneratedDispatchRoots.FindResultPlan(typeof(TMessage), typeof(TResult)) is { } plan)
        {
            return plan.Accept(
                GeneratedResultExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, plan.DirectHandlerFactory));
        }

        return new FrozenResultDispatch<TMessage, TResult>(dependenciesFactory, plan: null);
    }

    /// <summary>
    /// Immutable (cache, executor) pair, for the same reason its void twin is immutable: a
    /// reader that observes the reference sees both fields.
    /// </summary>
    private sealed class TypedResultExecutorSlot<TResult>(
        PipelineExecutorCache cache, IPipelineExecutor<TResult> executor)
    {
        public readonly PipelineExecutorCache Cache = cache;
        public readonly IPipelineExecutor<TResult> Executor = executor;
    }

    /// <summary>
    /// Per-(message, result) slot for the last cache instance that served a typed result
    /// lookup. Unlike <see cref="ResultExecutorSlot"/> — keyed by message type alone, so
    /// alternating result types on one message evict each other — this one is keyed by the
    /// pair, because the pair is what the type parameters already named.
    /// </summary>
    // Both type parameters are the cache key: one static slot per closed pair.
    // ReSharper disable once UnusedTypeParameter
    private static class ResultExecutorHolder<TMessage, TResult>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static TypedResultExecutorSlot<TResult>? Slot;
    }

    private IPipelineExecutor CreateVoidExecutor(Type messageType)
    {
        // Staged plan: bespoke code for the whole interceptor-bearing pipeline. Checked
        // before the single-handler plan — generation emits at most one plan kind per
        // message, and the staged one is the more specific claim. A plan is baked against
        // the unfiltered composition; the executor it hosts falls back to the plain shape
        // for a filtered dispatch rather than the table having to hand out a different one.
        if (GeneratedDispatchRoots.FindStagedVoidPlan(messageType) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedVoidExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, StagedPlan: stagedPlan));
        }

        // Generated void plan: closed over (message, handler) at compile time, so the
        // fast path calls the handler devirtualized.
        if (GeneratedDispatchRoots.FindVoidPlan(messageType) is { } plan)
        {
            return plan.Accept(
                GeneratedVoidExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, plan.DirectHandlerFactory));
        }

        // No plan claimed the message: the frozen runtime pipeline, closed over the
        // generated root when there is one and reflectively when there is not.
        return DispatchLookup.OverMessage(
            messageType,
            VoidExecutorVisitor.Instance,
            new ExecutorState(dependenciesFactory),
            typeof(FrozenVoidDispatch<>),
            [dependenciesFactory, null]);
    }

    private object CreateResultExecutor(Type messageType, Type resultType)
    {
        // Staged plan first, mirroring the void side.
        if (GeneratedDispatchRoots.FindStagedResultPlan(messageType, resultType) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedResultExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, StagedPlan: stagedPlan));
        }

        // Generated result plan: closed over (message, result, handler) at compile time,
        // so the fast path calls the handler devirtualized.
        if (GeneratedDispatchRoots.FindResultPlan(messageType, resultType) is { } plan)
        {
            return plan.Accept(
                GeneratedResultExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, plan.DirectHandlerFactory));
        }

        return DispatchLookup.OverResult(
            messageType,
            resultType,
            ResultExecutorVisitor.Instance,
            new ExecutorState(dependenciesFactory),
            typeof(FrozenResultDispatch<,>),
            [dependenciesFactory, null]);
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
        IMessageDependenciesFactory DependenciesFactory,
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
            => new FrozenVoidDispatch<TMessage>(state.DependenciesFactory, plan: null);
    }

    /// <summary>Result-executor counterpart of <see cref="VoidExecutorVisitor"/>.</summary>
    private sealed class ResultExecutorVisitor : IMessageResultRootVisitor<object, ExecutorState>
    {
        public static readonly ResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult>(ExecutorState state) where TMessage : IMessage
            => new FrozenResultDispatch<TMessage, TResult>(state.DependenciesFactory, plan: null);
    }

    /// <summary>
    /// Re-enters a generic context with a generated void plan's (message, handler) pair
    /// and constructs the plan-closed executor there — no reflection, and the handler
    /// call devirtualizes inside the closed generic.
    /// </summary>
    private sealed class GeneratedVoidExecutorVisitor : IVoidHandlerPlanVisitor<IPipelineExecutor, ExecutorState>
    {
        public static readonly GeneratedVoidExecutorVisitor Instance = new();

        public IPipelineExecutor Visit<TMessage, THandler>(ExecutorState state)
            where TMessage : IMessage
            where THandler : class, IAsyncHandler<TMessage>
            => new GeneratedVoidPipelineExecutor<TMessage, THandler>(
                state.DependenciesFactory,
                state.DirectHandlerFactory as Func<THandler>,
                state.DirectHandlerFactory as Func<IServiceProvider, THandler>);
    }

    /// <summary>
    /// Re-enters a generic context with a generated result plan's (message, result,
    /// handler) triple and constructs the plan-closed executor there; the result-producing
    /// counterpart of <see cref="GeneratedVoidExecutorVisitor"/>.
    /// </summary>
    private sealed class GeneratedResultExecutorVisitor : IResultHandlerPlanVisitor<object, ExecutorState>
    {
        public static readonly GeneratedResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult, THandler>(ExecutorState state)
            where TMessage : IMessage
            where THandler : class, IAsyncHandler<TMessage, TResult>
            => new GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
                state.DependenciesFactory,
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
            where TMessage : IMessage
            => new FrozenVoidDispatch<TMessage>(
                state.DependenciesFactory,
                (StagedVoidPlan<TMessage>)state.StagedPlan!);
    }

    /// <summary>Result-executor counterpart of <see cref="StagedVoidExecutorVisitor"/>.</summary>
    private sealed class StagedResultExecutorVisitor : IStagedResultPlanVisitor<object, ExecutorState>
    {
        public static readonly StagedResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult>(ExecutorState state)
            where TMessage : IMessage
            => new FrozenResultDispatch<TMessage, TResult>(
                state.DependenciesFactory,
                (StagedResultPlan<TMessage, TResult>)state.StagedPlan!);
    }
}
