using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Represents the resolved pipeline of a message type: the fixed set of handler and
/// interceptor references per stage, ordered and ready to resolve per dispatch.
/// </summary>
/// <remarks>
/// Instances are provider-independent and cached process-wide. Interceptor stages contain
/// both direct and indirect (assignable message type) registrations merged into a single
/// list — direct entries first, then indirect, each segment ordered by weight and handler
/// type name. Main handlers keep the direct/indirect split because mediation strategies
/// treat them differently (e.g. single-handler validation applies to direct handlers only).
/// </remarks>
public interface IMessageDependencies
{
    /// <summary>
    /// Gets the direct main handlers for the message.
    /// </summary>
    IReadOnlyList<IHandlerReference<IHandler>> Handlers { get; }

    /// <summary>
    /// Gets the indirect main handlers for the message (registered for an assignable message type).
    /// </summary>
    IReadOnlyList<IHandlerReference<IHandler>> IndirectHandlers { get; }

    /// <summary>
    /// Gets the pre-interceptors for the message (direct first, then indirect).
    /// </summary>
    IReadOnlyList<IHandlerReference<IPreInterceptor>> PreInterceptors { get; }

    /// <summary>
    /// Gets the post-interceptors for the message (direct first, then indirect).
    /// </summary>
    IReadOnlyList<IHandlerReference<IPostInterceptor>> PostInterceptors { get; }

    /// <summary>
    /// Gets the exception interceptors for the message (direct first, then indirect).
    /// </summary>
    IReadOnlyList<IHandlerReference<IExceptionInterceptor>> ExceptionInterceptors { get; }

    /// <summary>
    /// Gets the final interceptors for the message (direct first, then indirect).
    /// </summary>
    IReadOnlyList<IHandlerReference<IFinalInterceptor>> FinalInterceptors { get; }
}
