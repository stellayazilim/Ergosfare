using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// The resolved pipeline of a message type: fixed, ordered handler reference arrays per
/// stage, built once from the message's <see cref="FrozenPipelineShape"/> and shared
/// process-wide.
/// </summary>
/// <remarks>
/// Instances hold no scope state. Handler instances are resolved per invocation from the
/// dispatching scope's provider, which the mediation pipeline passes down explicitly —
/// unless <c>memoizedProvider</c> is supplied, in which case each reference resolves once
/// from that provider and caches the instance process-wide (the memoized fast path).
/// </remarks>
internal sealed class MessageDependencies : IMessageDependencies
{
    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IHandler>> Handlers { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IHandler>> IndirectHandlers { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IPreInterceptor>> PreInterceptors { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IPostInterceptor>> PostInterceptors { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IExceptionInterceptor>> ExceptionInterceptors { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IFinalInterceptor>> FinalInterceptors { get; }

    /// <summary>
    /// Initializes the fixed reference arrays from a message's frozen pipeline shape.
    /// </summary>
    /// <param name="shape">The ordered, group-filtered pipeline shape with pre-resolved handler types.</param>
    /// <param name="memoizedProvider">
    /// When non-null, references resolve once from this provider and cache the instance
    /// (memoized fast path); when null, references resolve per invocation from the
    /// execution context's provider.
    /// </param>
    public MessageDependencies(FrozenPipelineShape shape, IServiceProvider? memoizedProvider)
    {
        HandlerArray = Materialize<IHandler>(shape.Handlers, memoizedProvider);
        IndirectHandlerArray = Materialize<IHandler>(shape.IndirectHandlers, memoizedProvider);
        Handlers = HandlerArray;
        IndirectHandlers = IndirectHandlerArray;
        PreInterceptors = Materialize<IPreInterceptor>(shape.PreInterceptors, memoizedProvider);
        PostInterceptors = Materialize<IPostInterceptor>(shape.PostInterceptors, memoizedProvider);
        ExceptionInterceptors = Materialize<IExceptionInterceptor>(shape.ExceptionInterceptors, memoizedProvider);
        FinalInterceptors = Materialize<IFinalInterceptor>(shape.FinalInterceptors, memoizedProvider);

        // Precomputed once per (message type, groups): the exact condition the single-handler
        // strategies use for their zero-interceptor fast path. Executors read this to invoke
        // the handler directly, without entering the strategy's async machinery.
        HasNoInterceptors =
            PreInterceptors.Count == 0
            && PostInterceptors.Count == 0
            && ExceptionInterceptors.Count == 0
            && FinalInterceptors.Count == 0;

        // The direct level wins outright: a sole direct handler serves the message no
        // matter how many covariant candidates exist; without a direct one the dispatch
        // falls to the covariant level. Only a same-level contest — or an empty candidate
        // set — must reach the strategy to be told so, so the short circuit mirrors the
        // strategies' selection exactly.
        FastSingleHandler = HasNoInterceptors
            ? Handlers.Count == 1
                ? Handlers[0]
                : Handlers.Count == 0 && IndirectHandlers.Count == 1 ? IndirectHandlers[0] : null
            : null;
        MemoizedInstances = memoizedProvider is not null;
    }

    /// <summary>
    /// The main-handler stages as concrete arrays, so hot loops index without interface
    /// dispatch. Same instances the <see cref="Handlers"/>/<see cref="IndirectHandlers"/>
    /// properties expose.
    /// </summary>
    internal IHandlerReference<IHandler>[] HandlerArray { get; }
    internal IHandlerReference<IHandler>[] IndirectHandlerArray { get; }

    /// <summary>
    /// Whether all four interceptor stages are empty — the broadcast fast path's
    /// eligibility condition, computed once at construction.
    /// </summary>
    internal bool HasNoInterceptors { get; }

    /// <summary>
    /// The main handler the priority ladder selects — the sole direct one, else the sole
    /// covariant one — when the pipeline has no interceptor stages; <c>null</c> when the
    /// winning level is contested or empty. Computed once at construction.
    /// </summary>
    internal IHandlerReference<IHandler>? FastSingleHandler { get; }

    /// <summary>
    /// Whether references resolve once and cache the instance (memoized mode). Generated
    /// plans must not construct handlers directly in this mode — the memoized instance is
    /// the semantic contract.
    /// </summary>
    internal bool MemoizedInstances { get; }

    /// <summary>
    /// Wraps a shape's participant types in resolvable references. Runs once per
    /// (message type, groups) process-wide — never on the dispatch path.
    /// </summary>
    private static IHandlerReference<THandler>[] Materialize<THandler>(
        IReadOnlyList<Type> participants,
        IServiceProvider? memoizedProvider)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        var references = new IHandlerReference<THandler>[participants.Count];

        for (var i = 0; i < participants.Count; i++)
        {
            references[i] = new HandlerReference<THandler>(participants[i], memoizedProvider);
        }

        return references;
    }
}
