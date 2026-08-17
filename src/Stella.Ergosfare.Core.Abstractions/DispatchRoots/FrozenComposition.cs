using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// One participant of a frozen composition: the type to run, and the groups it runs under.
/// </summary>
/// <remarks>
/// Rows arrive already ordered — descending weight, then ordinal type name — so consuming
/// a composition only filters and closes them, never sorts.
/// </remarks>
/// <param name="handlerType">
/// The participant's type, which is a generic definition when the participant closes over
/// the message's type arguments at dispatch time. Its public constructors are preserved
/// under trimming: this is the only annotated point on the path from the generated table to
/// the container registrations that activate the type.
/// </param>
/// <param name="groups">
/// The groups the participant declared, or <c>null</c> for the default group alone — the
/// common case, carried without allocating an array.
/// </param>
public sealed class FrozenParticipant(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
    string[]? groups = null)
{
    /// <summary>
    /// The participant's type, possibly an open generic definition.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type HandlerType { get; } = handlerType;

    /// <summary>
    /// The declared groups; <c>null</c> means the default group alone.
    /// </summary>
    public IReadOnlyList<string>? Groups { get; } = groups;

    /// <summary>
    /// Reports whether this participant runs for a dispatch requesting
    /// <paramref name="effectiveGroups"/>.
    /// </summary>
    /// <param name="effectiveGroups">The groups the dispatch asked for; never empty.</param>
    /// <returns>
    /// <c>true</c> when any declared group ordinally equals any requested group.
    /// </returns>
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
/// A message's whole pipeline as the source generator compiled it: the two main-handler
/// segments and the four interceptor stages, each split into direct and indirect
/// participants, every segment already in execution order.
/// </summary>
/// <remarks>
/// One composition is emitted per message type. It is not yet a runnable pipeline — group
/// filtering and the closing of open participant types happen per dispatch, in
/// <see cref="BuildShape"/>, once per (message, group set). A message whose pipeline the
/// generator cannot model exactly has no composition at all.
/// </remarks>
/// <param name="messageType">The message type this composition was compiled for.</param>
/// <param name="handlers">The main handlers registered for the message type itself.</param>
/// <param name="indirectHandlers">The main handlers registered for a base type.</param>
/// <param name="preInterceptors">The direct pre-interceptors.</param>
/// <param name="indirectPreInterceptors">The covariantly matched pre-interceptors.</param>
/// <param name="postInterceptors">The direct post-interceptors.</param>
/// <param name="indirectPostInterceptors">The covariantly matched post-interceptors.</param>
/// <param name="exceptionInterceptors">The direct exception interceptors.</param>
/// <param name="indirectExceptionInterceptors">The covariantly matched exception interceptors.</param>
/// <param name="finalInterceptors">The direct final interceptors.</param>
/// <param name="indirectFinalInterceptors">The covariantly matched final interceptors.</param>
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
    /// <summary>
    /// The message type this composition was compiled for.
    /// </summary>
    public Type MessageType { get; } = messageType;

    /// <summary>
    /// The main handlers registered for the message type itself. An event carries every
    /// subscriber here.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> Handlers { get; } = handlers;

    /// <summary>
    /// The main handlers registered for a type the message is assignable to.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> IndirectHandlers { get; } = indirectHandlers;

    /// <summary>
    /// The pre-interceptors registered for the message type itself, in execution order.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> PreInterceptors { get; } = preInterceptors;

    /// <summary>
    /// The pre-interceptors registered for a type the message is assignable to.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> IndirectPreInterceptors { get; } = indirectPreInterceptors;

    /// <summary>
    /// The post-interceptors registered for the message type itself, in execution order.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> PostInterceptors { get; } = postInterceptors;

    /// <summary>
    /// The post-interceptors registered for a type the message is assignable to.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> IndirectPostInterceptors { get; } = indirectPostInterceptors;

    /// <summary>
    /// The exception interceptors registered for the message type itself, in execution order.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> ExceptionInterceptors { get; } = exceptionInterceptors;

    /// <summary>
    /// The exception interceptors registered for a type the message is assignable to.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> IndirectExceptionInterceptors { get; } = indirectExceptionInterceptors;

    /// <summary>
    /// The final interceptors registered for the message type itself, in execution order.
    /// </summary>
    public IReadOnlyList<FrozenParticipant> FinalInterceptors { get; } = finalInterceptors;

    /// <summary>
    /// The final interceptors registered for a type the message is assignable to.
    /// </summary>
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
    /// The direct main-handler rows as their backing array, for callers that rebuild
    /// segments rather than enumerate them.
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
    /// Derives the runnable pipeline for one dispatch: filters every segment by group,
    /// merges each interceptor stage with its direct participants first, and closes open
    /// participant types over the runtime message type's arguments.
    /// </summary>
    /// <param name="runtimeMessageType">The dispatched message's runtime type.</param>
    /// <param name="groups">
    /// The requested groups; an empty list means the default group.
    /// </param>
    /// <returns>The pipeline shape for this message and group set.</returns>
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

    /// <summary>
    /// Returns the participants of one segment that run for these groups, closed over the
    /// runtime message type.
    /// </summary>
    /// <param name="segment">The segment to filter.</param>
    /// <param name="runtimeMessageType">The dispatched message's runtime type.</param>
    /// <param name="effectiveGroups">The groups the dispatch asked for.</param>
    /// <returns>The surviving participant types, order preserved.</returns>
    private static Type[] FilterSegment(
        FrozenParticipant[] segment, Type runtimeMessageType, IReadOnlyList<string> effectiveGroups)
    {
        // Counted first so the result array is allocated exactly once, at its final size.
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

    /// <summary>
    /// Filters both segments of an interceptor stage and joins them, direct participants
    /// first.
    /// </summary>
    /// <param name="direct">The participants registered for the message type itself.</param>
    /// <param name="indirect">The participants registered for a base type.</param>
    /// <param name="runtimeMessageType">The dispatched message's runtime type.</param>
    /// <param name="effectiveGroups">The groups the dispatch asked for.</param>
    /// <returns>The stage's participant types, in execution order.</returns>
    private static Type[] MergeStage(
        FrozenParticipant[] direct, FrozenParticipant[] indirect,
        Type runtimeMessageType, IReadOnlyList<string> effectiveGroups)
    {
        var directTypes = FilterSegment(direct, runtimeMessageType, effectiveGroups);
        var indirectTypes = FilterSegment(indirect, runtimeMessageType, effectiveGroups);

        // With one segment empty the other already is the stage; only a genuine merge
        // allocates.
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

    /// <summary>
    /// Closes an open participant type over the runtime message type's arguments, or
    /// returns it unchanged when there is nothing to close.
    /// </summary>
    /// <param name="handlerType">The participant type from the composition.</param>
    /// <param name="runtimeMessageType">The dispatched message's runtime type.</param>
    /// <returns>The type to instantiate.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Open participants only close over generic message dispatches, which generated apps root at compile time.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The closed participant serves a live pipeline; the pipeline roots it.")]
    private static Type Close(Type handlerType, Type runtimeMessageType)
        => handlerType.IsGenericTypeDefinition && runtimeMessageType.IsGenericType
            ? handlerType.MakeGenericType(runtimeMessageType.GetGenericArguments())
            : handlerType;
}

/// <summary>
/// A message's runnable pipeline for one group set: the participant types of each stage,
/// filtered, closed and in execution order.
/// </summary>
/// <param name="handlers">The direct main-handler types.</param>
/// <param name="indirectHandlers">The covariantly matched main-handler types.</param>
/// <param name="preInterceptors">The merged pre-interceptor stage.</param>
/// <param name="postInterceptors">The merged post-interceptor stage.</param>
/// <param name="exceptionInterceptors">The merged exception-interceptor stage.</param>
/// <param name="finalInterceptors">The merged final-interceptor stage.</param>
public sealed class FrozenPipelineShape(
    Type[] handlers,
    Type[] indirectHandlers,
    Type[] preInterceptors,
    Type[] postInterceptors,
    Type[] exceptionInterceptors,
    Type[] finalInterceptors)
{
    /// <summary>
    /// The main handlers registered for the message type itself, in execution order.
    /// </summary>
    public IReadOnlyList<Type> Handlers { get; } = handlers;

    /// <summary>
    /// The main handlers registered for a base type, in execution order.
    /// </summary>
    public IReadOnlyList<Type> IndirectHandlers { get; } = indirectHandlers;

    /// <summary>
    /// The pre-interceptor stage, direct participants first.
    /// </summary>
    public IReadOnlyList<Type> PreInterceptors { get; } = preInterceptors;

    /// <summary>
    /// The post-interceptor stage, direct participants first.
    /// </summary>
    public IReadOnlyList<Type> PostInterceptors { get; } = postInterceptors;

    /// <summary>
    /// The exception-interceptor stage, direct participants first.
    /// </summary>
    public IReadOnlyList<Type> ExceptionInterceptors { get; } = exceptionInterceptors;

    /// <summary>
    /// The final-interceptor stage, direct participants first.
    /// </summary>
    public IReadOnlyList<Type> FinalInterceptors { get; } = finalInterceptors;
}
