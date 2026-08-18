using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// The participants of one message type and group set, as fixed per-stage arrays built once
/// from the message's pipeline shape.
/// </summary>
/// <remarks>
/// An instance holds no scope state and is shared across dispatches. Participant instances
/// come from the provider each dispatch passes in — unless the instance was built with a
/// memoized provider, in which case each reference resolves once and keeps its instance.
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
    /// Builds the per-stage reference arrays from a message's pipeline shape.
    /// </summary>
    /// <param name="shape">
    /// The message's pipeline for one group set: participant types per stage, in invocation
    /// order and already closed over the message's type arguments.
    /// </param>
    /// <param name="memoizedProvider">
    /// When supplied, references resolve once from this provider and keep the instance;
    /// when <c>null</c>, they resolve per invocation from the dispatching scope's provider.
    /// </param>
    /// <param name="forcedMemoization">
    /// Whether the memoization was demanded (<c>ForceMemoizedHandlers</c>) rather than
    /// derived from every participant being a singleton. Only the demanded kind bars a
    /// compiled plan: resolving a singleton per dispatch returns the one instance anyway,
    /// so the derived kind and a plan cannot be told apart.
    /// </param>
    public MessageDependencies(FrozenPipelineShape shape, IServiceProvider? memoizedProvider,
        bool forcedMemoization = false)
    {
        HandlerArray = Materialize<IHandler>(shape.Handlers, memoizedProvider);
        IndirectHandlerArray = Materialize<IHandler>(shape.IndirectHandlers, memoizedProvider);
        Handlers = HandlerArray;
        IndirectHandlers = IndirectHandlerArray;
        PreInterceptors = Materialize<IPreInterceptor>(shape.PreInterceptors, memoizedProvider);
        PostInterceptors = Materialize<IPostInterceptor>(shape.PostInterceptors, memoizedProvider);
        ExceptionInterceptors = Materialize<IExceptionInterceptor>(shape.ExceptionInterceptors, memoizedProvider);
        FinalInterceptors = Materialize<IFinalInterceptor>(shape.FinalInterceptors, memoizedProvider);

        // Worked out once per (message type, groups): the exact condition the single-handler
        // paths need before they can call a handler without entering a strategy at all.
        HasNoInterceptors =
            PreInterceptors.Count == 0
            && PostInterceptors.Count == 0
            && ExceptionInterceptors.Count == 0
            && FinalInterceptors.Count == 0;

        // Handlers registered for the message type itself win outright: one of those serves
        // the message however many covariant candidates exist, and only when there are none
        // does the covariant level get considered. A contested level — or no candidate at
        // all — has to reach the strategy to be reported, so this mirrors the strategies'
        // own selection exactly.
        FastSingleHandler = HasNoInterceptors
            ? Handlers.Count == 1
                ? Handlers[0]
                : Handlers.Count == 0 && IndirectHandlers.Count == 1 ? IndirectHandlers[0] : null
            : null;
        MemoizedInstances = memoizedProvider is not null;
        ForcedMemoization = forcedMemoization;
    }

    /// <summary>
    /// The direct main handlers as a concrete array, so hot loops index it without going
    /// through an interface. The same instance <see cref="Handlers"/> exposes.
    /// </summary>
    internal IHandlerReference<IHandler>[] HandlerArray { get; }

    /// <summary>
    /// The covariantly matched main handlers as a concrete array; the same instance
    /// <see cref="IndirectHandlers"/> exposes.
    /// </summary>
    internal IHandlerReference<IHandler>[] IndirectHandlerArray { get; }

    /// <summary>
    /// Whether all four interceptor stages are empty.
    /// </summary>
    internal bool HasNoInterceptors { get; }

    /// <summary>
    /// The one main handler this pipeline runs — the sole direct one, or the sole covariant
    /// one when there is no direct handler — but only when there are no interceptors at all.
    /// <c>null</c> when the winning level is empty or contested, or when a stage would run.
    /// </summary>
    internal IHandlerReference<IHandler>? FastSingleHandler { get; }

    /// <summary>
    /// Whether references resolve once and keep their instance.
    /// </summary>
    /// <remarks>
    /// Generated plans must not construct participants themselves in this mode: reusing the
    /// one instance is the contract.
    /// </remarks>
    internal bool MemoizedInstances { get; }

    /// <summary>
    /// Whether the memoization was demanded rather than derived from an all-singleton
    /// pipeline. Only this kind bars a compiled plan; see the constructor.
    /// </summary>
    internal bool ForcedMemoization { get; }

    /// <summary>
    /// Wraps a stage's participant types in resolvable references.
    /// </summary>
    /// <typeparam name="THandler">The stage's participant contract.</typeparam>
    /// <param name="participants">The participant types, in invocation order.</param>
    /// <param name="memoizedProvider">The provider to memoize against, or <c>null</c>.</param>
    /// <returns>The references, in the same order.</returns>
    /// <remarks>
    /// Runs once per (message type, groups) for the whole process, never during a dispatch.
    /// </remarks>
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
