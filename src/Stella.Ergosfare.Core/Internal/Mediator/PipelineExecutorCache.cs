using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's pipelines for sending, keyed by message type and — for the
/// result-producing ones — result type. The counterpart of the broadcast and stream tables.
/// </summary>
/// <param name="dependenciesFactory">The factory the pipelines resolve participants through.</param>
/// <remarks>
/// Groups are absent from every key here: one pipeline per message type serves every group
/// set and picks its participants per call, the way the publishing table has always worked.
/// What remains is the lookup itself — one dictionary per shape, with a last-used slot and
/// a compile-time slot in front of it that skip even that.
/// </remarks>
internal sealed class PipelineExecutorCache(IMessageDependenciesFactory dependenciesFactory)
{
    private readonly ConcurrentDictionary<Type, IPipelineExecutor> _voidExecutorsByType = new();
    private readonly ConcurrentDictionary<(Type MessageType, Type ResultType), object> _resultExecutorsByType = new();

    // The last result executor used for each message type. A message type almost always has
    // a single result type, so this turns the composite key's tuple hash into one Type
    // lookup plus a reference check. A miss falls through to the composite store, which
    // stays the authority — so a message dispatched with alternating result types keeps one
    // executor, and one participant cache, per pair.
    private readonly ConcurrentDictionary<Type, ResultExecutorSlot> _resultSlotsByType = new();

    /// <summary>
    /// A result type and the executor built for it.
    /// </summary>
    /// <param name="resultType">The result type the executor produces.</param>
    /// <param name="executor">The executor.</param>
    /// <remarks>
    /// Immutable, which is what makes refreshing the slot without locking safe: a reader
    /// that sees the reference sees both fields.
    /// </remarks>
    private sealed class ResultExecutorSlot(Type resultType, object executor)
    {
        public readonly Type ResultType = resultType;
        public readonly object Executor = executor;
    }

    /// <summary>
    /// Returns the void pipeline of a message type named at compile time.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>The pipeline for that type.</returns>
    /// <remarks>
    /// A static generic slot turns the dictionary lookup into a field read plus a check
    /// that the slot belongs to this table. Callers must first confirm
    /// <c>message.GetType() == typeof(TMessage)</c>; a base-typed call has to keep resolving
    /// by the runtime type. Group-filtered dispatches use this too, since groups are not
    /// part of a pipeline's identity.
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
    /// A table and the void executor it served.
    /// </summary>
    /// <param name="cache">The table the executor belongs to.</param>
    /// <param name="executor">The executor.</param>
    /// <remarks>
    /// Immutable, so refreshing the slot without locking is safe.
    /// </remarks>
    private sealed class VoidExecutorSlot(PipelineExecutorCache cache, IPipelineExecutor executor)
    {
        public readonly PipelineExecutorCache Cache = cache;
        public readonly IPipelineExecutor Executor = executor;
    }

    /// <summary>
    /// The last table to serve a compile-time void lookup for one message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type this slot belongs to.</typeparam>
    /// <remarks>
    /// Several containers alternating over one message type rewrite the slot each time,
    /// which is correct because every read checks the table; with a single container — every
    /// production process — the field is written once and read forever. The reference keeps
    /// the last serving table, and that container's pipelines, alive past disposal: one
    /// pipeline graph per message type, which in a single-container process is the live one.
    /// A weak reference would instead tax every read on the hot path.
    /// </remarks>
    // The type parameter is the key: one static slot per closed message type.
    // ReSharper disable once UnusedTypeParameter
    private static class VoidExecutorHolder<TMessage> where TMessage : IMessage
    {
        // ReSharper disable once StaticMemberInGenericType
        public static VoidExecutorSlot? Slot;
    }

    /// <summary>
    /// Returns the void pipeline of <paramref name="messageType"/>, building it on first
    /// use.
    /// </summary>
    /// <param name="messageType">The message's runtime type.</param>
    /// <returns>The pipeline for that type.</returns>
    public IPipelineExecutor GetVoidExecutor(Type messageType)
        => _voidExecutorsByType.TryGetValue(messageType, out var executor)
            ? executor
            : _voidExecutorsByType.GetOrAdd(messageType,
                static (t, cache) => cache.CreateVoidExecutor(t), this);

    /// <summary>
    /// Returns the pipeline of <paramref name="messageType"/> producing
    /// <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="messageType">The message's runtime type.</param>
    /// <returns>The pipeline for that pair.</returns>
    public IPipelineExecutor<TResult> GetExecutor<TResult>(Type messageType)
    {
        if (_resultSlotsByType.TryGetValue(messageType, out var slot)
            && ReferenceEquals(slot.ResultType, typeof(TResult)))
        {
            // A slot only ever holds an executor built as IPipelineExecutor<TResult> for the
            // result type recorded beside it, so this cast can skip the runtime variance
            // check the hot path would otherwise pay for.
            return Unsafe.As<IPipelineExecutor<TResult>>(slot.Executor);
        }

        return GetExecutorSlow<TResult>(messageType);
    }

    /// <summary>
    /// Resolves a result pipeline the slot did not hold, and refreshes the slot.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="messageType">The message's runtime type.</param>
    /// <returns>The pipeline for that pair.</returns>
    /// <remarks>
    /// Reached on the first dispatch of a message type, or when one message type is
    /// dispatched with alternating result types.
    /// </remarks>
    private IPipelineExecutor<TResult> GetExecutorSlow<TResult>(Type messageType)
    {
        var executor = (IPipelineExecutor<TResult>)_resultExecutorsByType.GetOrAdd(
            (messageType, typeof(TResult)),
            static (k, cache) => cache.CreateResultExecutor(k.MessageType, k.ResultType), this);

        _resultSlotsByType[messageType] = new ResultExecutorSlot(typeof(TResult), executor);

        return executor;
    }

    /// <summary>
    /// Returns the pipeline of a (message, result) pair named at compile time.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>The pipeline for that pair.</returns>
    /// <remarks>
    /// <para>
    /// Both types being compile-time constants, the pipeline comes from a static generic
    /// field rather than a tuple hash over two <see cref="Type"/> objects. Callers must
    /// first confirm <c>message.GetType() == typeof(TMessage)</c>.
    /// </para>
    /// <para>
    /// The check that the slot belongs to this table is not optional: a static generic field
    /// is process-wide while a pipeline belongs to one container, so without it two
    /// containers over the same pair — which every test class creates — would read each
    /// other's pipelines.
    /// </para>
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
    /// Resolves a compile-time-named result pipeline the slot did not hold, and refreshes
    /// both slots.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>The pipeline for that pair.</returns>
    /// <remarks>
    /// The composite store remains the authority, so a message dispatched both ways shares
    /// one pipeline and one participant cache; only how the pipeline is built differs.
    /// </remarks>
    private IPipelineExecutor<TResult> GetTypedExecutorSlow<TMessage, TResult>()
        where TMessage : IMessage
    {
        var executor = (IPipelineExecutor<TResult>)_resultExecutorsByType.GetOrAdd(
            (typeof(TMessage), typeof(TResult)),
            _ => CreateResultExecutor<TMessage, TResult>());

        // Both slots are written, so a later untyped dispatch of the same message finds this
        // executor through its own fast path instead of going to the dictionary.
        _resultSlotsByType[typeof(TMessage)] = new ResultExecutorSlot(typeof(TResult), executor);
        ResultExecutorHolder<TMessage, TResult>.Slot = new TypedResultExecutorSlot<TResult>(this, executor);

        return executor;
    }

    /// <summary>
    /// Builds a result pipeline from type arguments the compiler already knows.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>The pipeline for that pair.</returns>
    /// <remarks>
    /// The plan branches are the same as the runtime-typed path's, since a plan carries its
    /// own closed generics. Only the last branch differs: where that path asks the root
    /// table and, for a message without a root, closes a generic reflectively, here the
    /// closed type is the one the caller named — ordinary compiled code, with no root lookup
    /// and an answer Native AOT can give even for a message the generator never saw.
    /// </remarks>
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
    /// A table and the result executor it served.
    /// </summary>
    /// <typeparam name="TResult">The result type the executor produces.</typeparam>
    /// <param name="cache">The table the executor belongs to.</param>
    /// <param name="executor">The executor.</param>
    /// <remarks>Immutable, for the same reason its void twin is.</remarks>
    private sealed class TypedResultExecutorSlot<TResult>(
        PipelineExecutorCache cache, IPipelineExecutor<TResult> executor)
    {
        public readonly PipelineExecutorCache Cache = cache;
        public readonly IPipelineExecutor<TResult> Executor = executor;
    }

    /// <summary>
    /// The last table to serve a compile-time result lookup for one (message, result) pair.
    /// </summary>
    /// <typeparam name="TMessage">The message type this slot belongs to.</typeparam>
    /// <typeparam name="TResult">The result type this slot belongs to.</typeparam>
    /// <remarks>
    /// Keyed by the pair, unlike <see cref="ResultExecutorSlot"/> which is keyed by message
    /// type alone and so lets alternating result types evict each other — here both types
    /// were already named by the caller.
    /// </remarks>
    // Both type parameters are the key: one static slot per closed pair.
    // ReSharper disable once UnusedTypeParameter
    private static class ResultExecutorHolder<TMessage, TResult>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static TypedResultExecutorSlot<TResult>? Slot;
    }

    /// <summary>
    /// Builds the void pipeline of a message type, choosing the most specific compiled form
    /// available.
    /// </summary>
    /// <param name="messageType">The message's runtime type.</param>
    /// <returns>The pipeline for that type.</returns>
    private IPipelineExecutor CreateVoidExecutor(Type messageType)
    {
        // The staged plan is checked first because it is the more specific claim; generation
        // emits at most one kind of plan per message anyway. A plan is compiled against the
        // unfiltered pipeline, and the executor hosting it falls back to the general shape
        // for a filtered dispatch, so the table does not need a separate entry for that.
        if (GeneratedDispatchRoots.FindStagedVoidPlan(messageType) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedVoidExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, StagedPlan: stagedPlan));
        }

        // A single-handler plan, closed over the message and handler at compile time, so the
        // handler is called directly.
        if (GeneratedDispatchRoots.FindVoidPlan(messageType) is { } plan)
        {
            return plan.Accept(
                GeneratedVoidExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, plan.DirectHandlerFactory));
        }

        // No plan covers this message: the general pipeline, closed over the generated root
        // when there is one and reflectively when there is not.
        return DispatchLookup.OverMessage(
            messageType,
            VoidExecutorVisitor.Instance,
            new ExecutorState(dependenciesFactory),
            typeof(FrozenVoidDispatch<>),
            [dependenciesFactory, null]);
    }

    /// <summary>
    /// Builds the pipeline of a (message, result) pair, choosing the most specific compiled
    /// form available.
    /// </summary>
    /// <param name="messageType">The message's runtime type.</param>
    /// <param name="resultType">The result type.</param>
    /// <returns>The pipeline for that pair.</returns>
    private object CreateResultExecutor(Type messageType, Type resultType)
    {
        // Staged plan first, as on the void side.
        if (GeneratedDispatchRoots.FindStagedResultPlan(messageType, resultType) is { } stagedPlan)
        {
            return stagedPlan.Accept(
                StagedResultExecutorVisitor.Instance,
                new ExecutorState(dependenciesFactory, StagedPlan: stagedPlan));
        }

        // A single-handler plan, closed over message, result and handler at compile time.
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
    /// What a visitor needs to construct an executor inside a closed generic context.
    /// </summary>
    /// <param name="DependenciesFactory">The factory the executor resolves participants through.</param>
    /// <param name="DirectHandlerFactory">
    /// A plan's construction path — a <c>Func&lt;THandler&gt;</c> or
    /// <c>Func&lt;IServiceProvider, THandler&gt;</c>, held without its type and cast back
    /// inside the closed generic — or <c>null</c> for plain roots and plans that carry none.
    /// </param>
    /// <param name="StagedPlan">
    /// A staged plan, held without its type and cast back inside the closed generic, or
    /// <c>null</c> for every other root.
    /// </param>
    private readonly record struct ExecutorState(
        IMessageDependenciesFactory DependenciesFactory,
        object? DirectHandlerFactory = null,
        object? StagedPlan = null);

    /// <summary>
    /// Constructs a general void pipeline inside a generic context carrying the root's
    /// message type.
    /// </summary>
    private sealed class VoidExecutorVisitor : IMessageRootVisitor<IPipelineExecutor, ExecutorState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly VoidExecutorVisitor Instance = new();

        /// <inheritdoc />
        public IPipelineExecutor Visit<TMessage>(ExecutorState state) where TMessage : IMessage
            => new FrozenVoidDispatch<TMessage>(state.DependenciesFactory, plan: null);
    }

    /// <summary>
    /// Constructs a general result pipeline; the counterpart of
    /// <see cref="VoidExecutorVisitor"/>.
    /// </summary>
    private sealed class ResultExecutorVisitor : IMessageResultRootVisitor<object, ExecutorState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly ResultExecutorVisitor Instance = new();

        /// <inheritdoc />
        public object Visit<TMessage, TResult>(ExecutorState state) where TMessage : IMessage
            => new FrozenResultDispatch<TMessage, TResult>(state.DependenciesFactory, plan: null);
    }

    /// <summary>
    /// Constructs a single-handler void pipeline inside a generic context carrying the
    /// plan's message and handler types, so the handler call is direct.
    /// </summary>
    private sealed class GeneratedVoidExecutorVisitor : IVoidHandlerPlanVisitor<IPipelineExecutor, ExecutorState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly GeneratedVoidExecutorVisitor Instance = new();

        /// <inheritdoc />
        public IPipelineExecutor Visit<TMessage, THandler>(ExecutorState state)
            where TMessage : IMessage
            where THandler : class, IAsyncHandler<TMessage>
            => new GeneratedVoidPipelineExecutor<TMessage, THandler>(
                state.DependenciesFactory,
                state.DirectHandlerFactory as Func<THandler>,
                state.DirectHandlerFactory as Func<IServiceProvider, THandler>);
    }

    /// <summary>
    /// Constructs a single-handler result pipeline; the counterpart of
    /// <see cref="GeneratedVoidExecutorVisitor"/>.
    /// </summary>
    private sealed class GeneratedResultExecutorVisitor : IResultHandlerPlanVisitor<object, ExecutorState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly GeneratedResultExecutorVisitor Instance = new();

        /// <inheritdoc />
        public object Visit<TMessage, TResult, THandler>(ExecutorState state)
            where TMessage : IMessage
            where THandler : class, IAsyncHandler<TMessage, TResult>
            => new GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
                state.DependenciesFactory,
                state.DirectHandlerFactory as Func<THandler>,
                state.DirectHandlerFactory as Func<IServiceProvider, THandler>);
    }

    /// <summary>
    /// Constructs the executor that hosts a staged void plan, inside a generic context
    /// carrying the plan's message type.
    /// </summary>
    private sealed class StagedVoidExecutorVisitor : IStagedVoidPlanVisitor<IPipelineExecutor, ExecutorState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly StagedVoidExecutorVisitor Instance = new();

        /// <inheritdoc />
        public IPipelineExecutor Visit<TMessage>(ExecutorState state)
            where TMessage : IMessage
            => new FrozenVoidDispatch<TMessage>(
                state.DependenciesFactory,
                (StagedVoidPlan<TMessage>)state.StagedPlan!);
    }

    /// <summary>
    /// Constructs the executor that hosts a staged result plan; the counterpart of
    /// <see cref="StagedVoidExecutorVisitor"/>.
    /// </summary>
    private sealed class StagedResultExecutorVisitor : IStagedResultPlanVisitor<object, ExecutorState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly StagedResultExecutorVisitor Instance = new();

        /// <inheritdoc />
        public object Visit<TMessage, TResult>(ExecutorState state)
            where TMessage : IMessage
            => new FrozenResultDispatch<TMessage, TResult>(
                state.DependenciesFactory,
                (StagedResultPlan<TMessage, TResult>)state.StagedPlan!);
    }
}
