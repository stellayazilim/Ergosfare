using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// One participant row of a frozen composition: the handler type plus the group names it
/// participates under. Rows are emitted pre-sorted (weight descending, then ordinal
/// <c>Type.FullName</c> — the runtime shape-builder's exact comparator), so consumption
/// only ever filters and closes, never sorts.
/// </summary>
/// <param name="handlerType">
/// The participant's type — a generic definition when the participant closes over the
/// message's type arguments at dispatch time. Public constructors are preserved under
/// trimming: the generated table hands its <c>typeof</c> here, and this parameter is the
/// only annotated slot on the path to the container's handler registrations, which
/// activate the type reflectively.
/// </param>
/// <param name="groups">
/// The participant's declared group names, or <c>null</c> for the default group alone —
/// the overwhelmingly common case, carried without an allocation.
/// </param>
public sealed class FrozenParticipant(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
    string[]? groups = null)
{
    /// <summary>The participant's (possibly open) type.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type HandlerType { get; } = handlerType;

    /// <summary>The declared group names; <c>null</c> means the default group alone.</summary>
    public IReadOnlyList<string>? Groups { get; } = groups;

    /// <summary>
    /// The runtime group predicate: any of the participant's groups ordinally equals any
    /// requested group — the shape-builder's <c>MatchesAnyGroup</c>, mirrored.
    /// </summary>
    internal bool MatchesAnyGroup(IReadOnlyList<string> effectiveGroups)
    {
        if (Groups is null)
        {
            for (var i = 0; i < effectiveGroups.Count; i++)
            {
                if (string.Equals(effectiveGroups[i], Attributes.GroupAttribute.DefaultGroupName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        for (var i = 0; i < Groups.Count; i++)
        {
            for (var j = 0; j < effectiveGroups.Count; j++)
            {
                if (string.Equals(Groups[i], effectiveGroups[j], StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// A message's whole frozen pipeline composition — the compile-time image of what the
/// runtime shape-builder derives from the registry: the two main-handler segments
/// (events carry N rows) and the four interceptor stages as direct/indirect segment
/// pairs, every segment in the runtime execution order. Emitted per message by the
/// source generator; group filtering happens per dispatch through
/// <see cref="BuildShape"/>, once per (message, group set).
/// </summary>
/// <remarks>
/// This is Phase 2's parallel surface: the registry remains the dispatch authority and
/// the advisory gates stay in place, while dual-run parity tests hold this table to the
/// shape-builder's output. Phase 3 turns it into the sole source. Messages whose
/// composition the generator cannot model exactly — keyed participants, pipeline
/// exclusions, unprovable ordering ties — simply have no entry and stay on the registry.
/// </remarks>
public sealed class FrozenComposition(
    Type messageType,
    FrozenParticipant[] handlers,
    FrozenParticipant[] indirectHandlers,
    FrozenParticipant[] preInterceptors,
    FrozenParticipant[] indirectPreInterceptors,
    FrozenParticipant[] postInterceptors,
    FrozenParticipant[] indirectPostInterceptors,
    FrozenParticipant[] exceptionInterceptors,
    FrozenParticipant[] indirectExceptionInterceptors,
    FrozenParticipant[] finalInterceptors,
    FrozenParticipant[] indirectFinalInterceptors)
{
    /// <summary>The message type the composition was baked for.</summary>
    public Type MessageType { get; } = messageType;

    /// <summary>The direct main-handler rows; events carry every subscriber here.</summary>
    public IReadOnlyList<FrozenParticipant> Handlers { get; } = handlers;

    /// <summary>The covariantly matched main-handler rows.</summary>
    public IReadOnlyList<FrozenParticipant> IndirectHandlers { get; } = indirectHandlers;

    /// <summary>The four interceptor stages as direct/indirect segment pairs, in execution order.</summary>
    public IReadOnlyList<FrozenParticipant> PreInterceptors { get; } = preInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> IndirectPreInterceptors { get; } = indirectPreInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> PostInterceptors { get; } = postInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> IndirectPostInterceptors { get; } = indirectPostInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> ExceptionInterceptors { get; } = exceptionInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> IndirectExceptionInterceptors { get; } = indirectExceptionInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> FinalInterceptors { get; } = finalInterceptors;

    /// <inheritdoc cref="PreInterceptors"/>
    public IReadOnlyList<FrozenParticipant> IndirectFinalInterceptors { get; } = indirectFinalInterceptors;

    private readonly FrozenParticipant[] _handlers = handlers;
    private readonly FrozenParticipant[] _indirectHandlers = indirectHandlers;
    private readonly FrozenParticipant[] _preInterceptors = preInterceptors;
    private readonly FrozenParticipant[] _indirectPreInterceptors = indirectPreInterceptors;
    private readonly FrozenParticipant[] _postInterceptors = postInterceptors;
    private readonly FrozenParticipant[] _indirectPostInterceptors = indirectPostInterceptors;
    private readonly FrozenParticipant[] _exceptionInterceptors = exceptionInterceptors;
    private readonly FrozenParticipant[] _indirectExceptionInterceptors = indirectExceptionInterceptors;
    private readonly FrozenParticipant[] _finalInterceptors = finalInterceptors;
    private readonly FrozenParticipant[] _indirectFinalInterceptors = indirectFinalInterceptors;

    /// <summary>
    /// The ten segments as their backing arrays, for the per-container selection
    /// projection — which needs to compare and rebuild them, not just enumerate.
    /// </summary>
    internal FrozenParticipant[] HandlerRows => _handlers;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] IndirectHandlerRows => _indirectHandlers;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] PreInterceptorRows => _preInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] IndirectPreInterceptorRows => _indirectPreInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] PostInterceptorRows => _postInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] IndirectPostInterceptorRows => _indirectPostInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] ExceptionInterceptorRows => _exceptionInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] IndirectExceptionInterceptorRows => _indirectExceptionInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] FinalInterceptorRows => _finalInterceptors;

    /// <inheritdoc cref="HandlerRows"/>
    internal FrozenParticipant[] IndirectFinalInterceptorRows => _indirectFinalInterceptors;

    /// <summary>
    /// Derives the executable pipeline shape for one dispatch: the shape-builder's exact
    /// semantics — an empty group request becomes the default group and a non-empty one
    /// is used verbatim, each segment filters by the any-of × any-of ordinal group
    /// predicate, the interceptor stages merge direct-segment-first, and open participant
    /// types close over the runtime message type's arguments.
    /// </summary>
    /// <param name="runtimeMessageType">The dispatched message's runtime type.</param>
    /// <param name="groups">The requested group names; empty means the default group.</param>
    public FrozenPipelineShape BuildShape(Type runtimeMessageType, IReadOnlyList<string> groups)
    {
        IReadOnlyList<string> effectiveGroups = groups.Count == 0
            ? [Attributes.GroupAttribute.DefaultGroupName]
            : groups;

        return new FrozenPipelineShape(
            FilterSegment(_handlers, runtimeMessageType, effectiveGroups),
            FilterSegment(_indirectHandlers, runtimeMessageType, effectiveGroups),
            MergeStage(_preInterceptors, _indirectPreInterceptors, runtimeMessageType, effectiveGroups),
            MergeStage(_postInterceptors, _indirectPostInterceptors, runtimeMessageType, effectiveGroups),
            MergeStage(_exceptionInterceptors, _indirectExceptionInterceptors, runtimeMessageType, effectiveGroups),
            MergeStage(_finalInterceptors, _indirectFinalInterceptors, runtimeMessageType, effectiveGroups));
    }

    private static Type[] FilterSegment(
        FrozenParticipant[] segment, Type runtimeMessageType, IReadOnlyList<string> effectiveGroups)
    {
        var count = 0;

        foreach (var participant in segment)
        {
            if (participant.MatchesAnyGroup(effectiveGroups))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Type.EmptyTypes;
        }

        var result = new Type[count];
        var index = 0;

        foreach (var participant in segment)
        {
            if (participant.MatchesAnyGroup(effectiveGroups))
            {
                result[index++] = Close(participant.HandlerType, runtimeMessageType);
            }
        }

        return result;
    }

    private static Type[] MergeStage(
        FrozenParticipant[] direct, FrozenParticipant[] indirect,
        Type runtimeMessageType, IReadOnlyList<string> effectiveGroups)
    {
        var directTypes = FilterSegment(direct, runtimeMessageType, effectiveGroups);
        var indirectTypes = FilterSegment(indirect, runtimeMessageType, effectiveGroups);

        if (indirectTypes.Length == 0)
        {
            return directTypes;
        }

        if (directTypes.Length == 0)
        {
            return indirectTypes;
        }

        var merged = new Type[directTypes.Length + indirectTypes.Length];
        directTypes.CopyTo(merged, 0);
        indirectTypes.CopyTo(merged, directTypes.Length);
        return merged;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Open participants only close over generic message dispatches, which generated apps root at compile time.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The closed participant serves a live pipeline; the pipeline roots it.")]
    private static Type Close(Type handlerType, Type runtimeMessageType)
        => handlerType.IsGenericTypeDefinition && runtimeMessageType.IsGenericType
            ? handlerType.MakeGenericType(runtimeMessageType.GetGenericArguments())
            : handlerType;
}

/// <summary>
/// The six executable stage arrays <see cref="FrozenComposition.BuildShape"/> derives for
/// one (message, group set) — the frozen mirror of the runtime pipeline shape: main
/// handlers split direct/indirect, interceptor stages merged direct-segment-first.
/// </summary>
public sealed class FrozenPipelineShape(
    Type[] handlers,
    Type[] indirectHandlers,
    Type[] preInterceptors,
    Type[] postInterceptors,
    Type[] exceptionInterceptors,
    Type[] finalInterceptors)
{
    /// <summary>The direct main-handler types, in execution order.</summary>
    public IReadOnlyList<Type> Handlers { get; } = handlers;

    /// <summary>The covariantly matched main-handler types, in execution order.</summary>
    public IReadOnlyList<Type> IndirectHandlers { get; } = indirectHandlers;

    /// <summary>The merged pre-interceptor stage, direct segment first.</summary>
    public IReadOnlyList<Type> PreInterceptors { get; } = preInterceptors;

    /// <summary>The merged post-interceptor stage, direct segment first.</summary>
    public IReadOnlyList<Type> PostInterceptors { get; } = postInterceptors;

    /// <summary>The merged exception-interceptor stage, direct segment first.</summary>
    public IReadOnlyList<Type> ExceptionInterceptors { get; } = exceptionInterceptors;

    /// <summary>The merged final-interceptor stage, direct segment first.</summary>
    public IReadOnlyList<Type> FinalInterceptors { get; } = finalInterceptors;
}
