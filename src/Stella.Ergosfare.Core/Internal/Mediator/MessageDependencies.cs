using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// The resolved pipeline of a message type: fixed, ordered handler reference arrays per
/// stage, built once from the cached <see cref="MessagePipelineShape"/> and shared
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
    public IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> Handlers { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> IndirectHandlers { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IPreInterceptor, IPreInterceptorDescriptor>> PreInterceptors { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IPostInterceptor, IPostInterceptorDescriptor>> PostInterceptors { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IExceptionInterceptor, IExceptionInterceptorDescriptor>> ExceptionInterceptors { get; }

    /// <inheritdoc />
    public IReadOnlyList<IHandlerReference<IFinalInterceptor, IFinalInterceptorDescriptor>> FinalInterceptors { get; }

    /// <summary>
    /// Convenience constructor building the shape on the fly and pinning resolution to the
    /// given provider (memoized mode). Intended for tests; production code goes through
    /// the cached-shape constructor.
    /// </summary>
    /// <param name="messageType">The type of the message for which dependencies are resolved.</param>
    /// <param name="descriptor">The message descriptor providing handler metadata.</param>
    /// <param name="serviceProvider">The provider handler instances are resolved from.</param>
    /// <param name="groups">The groups to filter handlers by; if none provided, the default group is used.</param>
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
        : this(MessagePipelineShape.Create(messageType, descriptor, groups), serviceProvider)
    {
    }

    /// <summary>
    /// Initializes the fixed reference arrays from a (cached) pipeline shape.
    /// </summary>
    /// <param name="shape">The ordered, group-filtered pipeline shape with pre-resolved handler types.</param>
    /// <param name="memoizedProvider">
    /// When non-null, references resolve once from this provider and cache the instance
    /// (memoized fast path); when null, references resolve per invocation from the
    /// execution context's provider.
    /// </param>
    public MessageDependencies(MessagePipelineShape shape, IServiceProvider? memoizedProvider)
    {
        HandlerArray = Materialize<IHandler, IMainHandlerDescriptor>(shape.Handlers, memoizedProvider);
        IndirectHandlerArray = Materialize<IHandler, IMainHandlerDescriptor>(shape.IndirectHandlers, memoizedProvider);
        Handlers = HandlerArray;
        IndirectHandlers = IndirectHandlerArray;
        PreInterceptors = Materialize<IPreInterceptor, IPreInterceptorDescriptor>(shape.PreInterceptors, memoizedProvider);
        PostInterceptors = Materialize<IPostInterceptor, IPostInterceptorDescriptor>(shape.PostInterceptors, memoizedProvider);
        ExceptionInterceptors = Materialize<IExceptionInterceptor, IExceptionInterceptorDescriptor>(shape.ExceptionInterceptors, memoizedProvider);
        FinalInterceptors = Materialize<IFinalInterceptor, IFinalInterceptorDescriptor>(shape.FinalInterceptors, memoizedProvider);

        // Precomputed once per (message type, groups): the exact condition the single-handler
        // strategies use for their zero-interceptor fast path. Executors read this to invoke
        // the handler directly, without entering the strategy's async machinery.
        HasNoInterceptors =
            PreInterceptors.Count == 0
            && PostInterceptors.Count == 0
            && ExceptionInterceptors.Count == 0
            && FinalInterceptors.Count == 0;

        // The candidate set the single-handler strategies resolve against is direct plus
        // indirect, so the executors' short circuit has to count both — a message with one
        // direct and one covariantly matched handler is contested, and must reach the
        // strategy to be told so rather than quietly running the direct one.
        FastSingleHandler = HasNoInterceptors && Handlers.Count + IndirectHandlers.Count == 1
            ? Handlers.Count == 1 ? Handlers[0] : IndirectHandlers[0]
            : null;
        MemoizedInstances = memoizedProvider is not null;
    }

    /// <summary>
    /// The main-handler stages as concrete arrays, so hot loops index without interface
    /// dispatch. Same instances the <see cref="Handlers"/>/<see cref="IndirectHandlers"/>
    /// properties expose.
    /// </summary>
    internal IHandlerReference<IHandler, IMainHandlerDescriptor>[] HandlerArray { get; }
    internal IHandlerReference<IHandler, IMainHandlerDescriptor>[] IndirectHandlerArray { get; }

    /// <summary>
    /// Whether all four interceptor stages are empty — the broadcast fast path's
    /// eligibility condition, computed once at construction.
    /// </summary>
    internal bool HasNoInterceptors { get; }

    /// <summary>
    /// The sole main handler — direct or covariantly matched — when the pipeline has
    /// exactly one and no interceptor stages; <c>null</c> otherwise. Computed once at
    /// construction.
    /// </summary>
    internal IHandlerReference<IHandler, IMainHandlerDescriptor>? FastSingleHandler { get; }

    /// <summary>
    /// Whether references resolve once and cache the instance (memoized mode). Generated
    /// plans must not construct handlers directly in this mode — the memoized instance is
    /// the semantic contract.
    /// </summary>
    internal bool MemoizedInstances { get; }

    /// <summary>
    /// Wraps the shape's planned handlers in resolvable references. Runs once per
    /// (message type, groups) process-wide — never on the dispatch path.
    /// </summary>
    private static IHandlerReference<THandler, TDescriptor>[] Materialize<THandler, TDescriptor>(
        PlannedHandler<TDescriptor>[] plannedHandlers,
        IServiceProvider? memoizedProvider) where TDescriptor : IHandlerDescriptor
    {
        if (plannedHandlers.Length == 0)
        {
            return [];
        }

        var references = new IHandlerReference<THandler, TDescriptor>[plannedHandlers.Length];

        for (var i = 0; i < plannedHandlers.Length; i++)
        {
            references[i] = new HandlerReference<THandler, TDescriptor>(
                plannedHandlers[i].Descriptor,
                plannedHandlers[i].HandlerType,
                memoizedProvider);
        }

        return references;
    }
}
