
namespace Stella.Ergosfare.Core.Abstractions.Planning;

/// <summary>
/// One participant of a frozen composition: the type to run, and the groups it runs under.
/// </summary>
/// <remarks>
/// Rows arrive already ordered — descending weight, then ordinal type name — so startup validation never sorts them.
/// </remarks>
/// <param name="handlerType">
/// The statically known participant type. Its generated typed DI registration preserves its public constructors.
/// </param>
/// <param name="groups">
/// The groups the participant declared, or <c>null</c> for the default group alone — the
/// common case, carried without allocating an array.
/// </param>
public sealed class FrozenParticipant(
    Type handlerType,
    string[]? groups = null)
{
    /// <summary>
    /// The statically known participant type.
    /// </summary>
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
/// Runtime metadata describing a generated pipeline's participants, groups and ordering.
/// The two handler segments and four interceptor stages distinguish direct and indirect
/// participants. Execution belongs to the separately generated plan.
/// </summary>
/// <remarks>
/// One descriptor is emitted per message type. Executable plans are generated separately;
/// this metadata supports registration binding and failure diagnostics only.
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
public sealed class PipelineDescriptor(
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

}
