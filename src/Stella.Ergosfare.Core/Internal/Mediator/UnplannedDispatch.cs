using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Builds the exception a dispatch raises when no compiled plan serves it. Nothing is
/// dispatched at run time that was not produced at compile time, so every route that used
/// to degrade into a runtime lane ends here instead.
/// </summary>
/// <remarks>
/// The failure stays as precise as the lane it replaced: a message nobody serves is still
/// <see cref="NoHandlerFoundException"/>, a contested one is still
/// <see cref="MultipleHandlerFoundException"/>, and only a pipeline that would have run —
/// had a plan been compiled for it — raises <see cref="UnplannedDispatchException"/>.
/// </remarks>
internal static class UnplannedDispatch
{
    /// <summary>
    /// The failure of a send with participants but no compiled plan.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="dependencies">The participants the container resolved.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForMissingPlan(Type messageType, IMessageDependencies dependencies)
        => Contest(messageType, dependencies)
           ?? new UnplannedDispatchException(
               messageType,
               UnplannedDispatchReason.NoCompiledPlan,
               $"No compiled plan serves '{messageType}'. The source generator did not model this dispatch — " +
               "keyed discovery, a synchronous main-handler contract, a contested or covariant claim, an " +
               "excluded type, or a runtime-only registration are the usual reasons. Nothing is dispatched at " +
               "run time that was not produced at compile time; make the pipeline plannable, or stop " +
               "dispatching the message.");

    /// <summary>
    /// The failure of a publish whose event has subscribers but no compiled plan.
    /// </summary>
    /// <param name="eventType">The event type being published.</param>
    /// <returns>The exception to throw.</returns>
    /// <remarks>
    /// No contest test here: several handlers on an event is the normal shape, not a fault.
    /// </remarks>
    internal static Exception ForMissingBroadcastPlan(Type eventType)
        => new UnplannedDispatchException(
            eventType,
            UnplannedDispatchReason.NoCompiledPlan,
            $"No compiled plan serves a publish of '{eventType}', and the event has subscribers that would " +
            "silently go unserved. The source generator did not model this publish — keyed discovery, a " +
            "subscriber shape it cannot call, an excluded type, or a runtime-only registration are the usual " +
            "reasons. Nothing is dispatched at run time that was not produced at compile time; make the " +
            "pipeline plannable, or stop publishing the event.");

    /// <summary>
    /// The failure of a dispatch whose live pipeline diverged from the compiled one.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="dependencies">The participants the container resolved.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForDivergedComposition(
        Type messageType, MessageDependencies dependencies, StagedPlanKey composition)
        => Contest(messageType, dependencies)
           ?? new UnplannedDispatchException(
               messageType,
               UnplannedDispatchReason.CompositionDiverged,
               $"The live pipeline for '{messageType}' is not the one its compiled plan was baked against — " +
               $"{StagedPlanGate.DescribeMismatch(dependencies, composition)}. A participant that exists only " +
               "at run time never enters a compiled plan, so it would silently not run. Declare it where the " +
               "generator can see it and rebuild, or take it out.");

    /// <summary>
    /// The failure of a publish whose live pipeline diverged from the compiled one.
    /// </summary>
    /// <param name="eventType">The event type being published.</param>
    /// <param name="dependencies">The participants the container resolved.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForDivergedBroadcastComposition(
        Type eventType, MessageDependencies dependencies, StagedPlanKey composition)
        => new UnplannedDispatchException(
            eventType,
            UnplannedDispatchReason.CompositionDiverged,
            $"The live pipeline for '{eventType}' is not the one its compiled plan was baked against — " +
            $"{StagedPlanGate.DescribeMismatch(dependencies, composition)}. A participant that exists only " +
            "at run time never enters a compiled plan, so it would silently not run. Declare it where the " +
            "generator can see it and rebuild, or take it out.");

    /// <summary>
    /// The failure of a dispatch whose live pipeline is no longer the single planned
    /// handler and nothing else.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="plannedHandlerType">The one handler the plan named.</param>
    /// <param name="dependencies">The participants the container resolved.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForDivergedHandlerPlan(
        Type messageType, Type plannedHandlerType, MessageDependencies dependencies)
        => Contest(messageType, dependencies)
           ?? new UnplannedDispatchException(
               messageType,
               UnplannedDispatchReason.CompositionDiverged,
               $"The live pipeline for '{messageType}' is not the one its compiled plan was baked against — " +
               $"the plan names '{plannedHandlerType}' alone, live [{DescribePipeline(dependencies)}]. A " +
               "participant that exists only at run time never enters a compiled plan, so it would silently " +
               "not run. Declare it where the generator can see it and rebuild, or take it out.");

    /// <summary>
    /// The failure of a dispatch whose container binds the planned handler type to
    /// something that is not the planned handler.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="plannedHandlerType">The one handler the plan named.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForDivergedHandlerRegistration(Type messageType, Type plannedHandlerType)
        => new UnplannedDispatchException(
            messageType,
            UnplannedDispatchReason.CompositionDiverged,
            $"The container resolves '{plannedHandlerType}' — the one handler the compiled plan for " +
            $"'{messageType}' names — to an instance of another type, so the pipeline in hand is not the " +
            "compiled one. Register the handler as itself, or declare the replacement where the generator " +
            "can see it and rebuild.");

    /// <summary>
    /// The failure of a dispatch whose pipeline memoizes participant instances.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForMemoizedInstances(Type messageType)
        => new UnplannedDispatchException(
            messageType,
            UnplannedDispatchReason.MemoizedInstances,
            $"The pipeline for '{messageType}' memoizes participant instances (ForceMemoizedHandlers), and a " +
            "compiled plan resolves or constructs its participants fresh — the two contracts cannot both " +
            "hold. Drop the memoization and register the participants with the lifetime you want; the " +
            "container will honor it inside the plan.");

    /// <summary>
    /// The failure of a dispatch whose bound result adapter is not the one the plan was
    /// compiled against.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="compiledAdapterType">The adapter the plan assumed, or <c>null</c> for none.</param>
    /// <param name="liveAdapterType">The adapter bound in this container, or <c>null</c> for none.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForResultAdapterMismatch(
        Type messageType, Type? compiledAdapterType, Type? liveAdapterType)
        => new UnplannedDispatchException(
            messageType,
            UnplannedDispatchReason.UnplannedResultAdapter,
            $"The result adapter bound to '{messageType}' is not the one its compiled plan was baked against " +
            $"— compiled {Describe(compiledAdapterType)}, live {Describe(liveAdapterType)}. An adapter the " +
            "plan does not know would silently not run its branches. Bind the adapter where the generator " +
            "can see it and rebuild, or unbind it.");

    /// <summary>
    /// The failure of a dispatch running under a dependencies factory the engine does not
    /// know.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForForeignFactory(Type messageType)
        => new UnplannedDispatchException(
            messageType,
            UnplannedDispatchReason.ForeignDependenciesFactory,
            $"The dispatch of '{messageType}' runs under a custom dependencies factory, so the pipeline it " +
            "would produce cannot be verified against any compiled plan. Nothing is dispatched at run time " +
            "that was not produced at compile time; use the module registry's own factory.");

    /// <summary>
    /// The failure of a dispatch that named a group set no compiled plan serves.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="groups">The group set the dispatch named.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForUnplannedGroupSet(Type messageType, IReadOnlyList<string> groups)
        => new UnplannedDispatchException(
            messageType,
            UnplannedDispatchReason.UnplannedGroupSet,
            $"The dispatch of '{messageType}' named the group set [{string.Join(", ", groups)}], and no " +
            "compiled plan serves that set — no per-set plan was emitted for it, and the filtering plan " +
            "cannot be verified for this container. Nothing is dispatched at run time that was not produced " +
            "at compile time; spell the set where the generator can see it, or make the filtering plan's " +
            "pipeline match this container.");

    /// <summary>
    /// The failure of a dispatch whose message type has no generated dispatch root.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception ForMissingDispatchRoot(Type messageType)
        => new UnplannedDispatchException(
            messageType,
            UnplannedDispatchReason.NoDispatchRoot,
            $"'{messageType}' has no generated dispatch root — the source generator never saw the type, so " +
            "nothing was compiled for it and nothing will dispatch it at run time. Declare the type where " +
            "the generator can see it and rebuild.");

    /// <summary>
    /// The contest or absence a planless send still reports precisely, or <c>null</c> when
    /// exactly one level serves the message with exactly one claimant.
    /// </summary>
    /// <param name="messageType">The message type being dispatched.</param>
    /// <param name="dependencies">The participants the container resolved.</param>
    /// <returns>The precise exception, or <c>null</c>.</returns>
    private static Exception? Contest(Type messageType, IMessageDependencies dependencies)
    {
        var handlers = dependencies.Handlers;
        var indirectHandlers = dependencies.IndirectHandlers;

        if (handlers.Count > 1)
        {
            return new MultipleHandlerFoundException(messageType, handlers.Count);
        }

        if (handlers.Count == 0 && indirectHandlers.Count > 1)
        {
            return new MultipleHandlerFoundException(messageType, indirectHandlers.Count);
        }

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
        {
            return new NoHandlerFoundException(messageType, $"No handler is registered for {messageType.Name}.");
        }

        return null;
    }

    /// <summary>
    /// Names an adapter type for an exception message.
    /// </summary>
    /// <param name="adapterType">The adapter type, or <c>null</c> for none.</param>
    /// <returns>The clause.</returns>
    private static string Describe(Type? adapterType)
        => adapterType is null ? "none" : $"'{adapterType}'";

    /// <summary>
    /// Names a live pipeline's shape for an exception message.
    /// </summary>
    /// <param name="dependencies">The participants the container resolved.</param>
    /// <returns>The clause.</returns>
    private static string DescribePipeline(MessageDependencies dependencies)
    {
        var clauses = new List<string>(2);

        if (dependencies.Handlers.Count > 0)
        {
            clauses.Add("handlers " + string.Join(", ", dependencies.Handlers.Select(reference => reference.HandlerType.Name)));
        }

        if (dependencies.IndirectHandlers.Count > 0)
        {
            clauses.Add("indirect handlers " + string.Join(", ", dependencies.IndirectHandlers.Select(reference => reference.HandlerType.Name)));
        }

        if (!dependencies.HasNoInterceptors)
        {
            clauses.Add("interceptors present");
        }

        return clauses.Count == 0 ? "nothing" : string.Join("; ", clauses);
    }
}
