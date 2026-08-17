using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The participants resolved for one message type, grouped by pipeline stage and ordered
/// as they will be invoked.
/// </summary>
/// <remarks>
/// <para>
/// An instance holds no provider and is cached per message type, so the same instance
/// serves every dispatch of that type; the participant instances themselves are resolved
/// per dispatch through <see cref="IHandlerReference{THandler}.Resolve"/>.
/// </para>
/// <para>
/// A participant is <em>direct</em> when it was registered for the dispatched message type
/// itself, and <em>indirect</em> when it was registered for a type the message is
/// assignable to. Interceptor stages merge both into one list — every direct entry first,
/// then every indirect one, each run ordered by descending weight and then by type name.
/// Main handlers keep the two apart, because mediation strategies treat them differently.
/// </para>
/// </remarks>
public interface IMessageDependencies
{
    /// <summary>
    /// The main handlers registered for the message type itself.
    /// </summary>
    IReadOnlyList<IHandlerReference<IHandler>> Handlers { get; }

    /// <summary>
    /// The main handlers registered for a type the message is assignable to.
    /// </summary>
    IReadOnlyList<IHandlerReference<IHandler>> IndirectHandlers { get; }

    /// <summary>
    /// The pre-interceptors to run before the main handlers, direct entries first.
    /// </summary>
    IReadOnlyList<IHandlerReference<IPreInterceptor>> PreInterceptors { get; }

    /// <summary>
    /// The post-interceptors to run after the main handlers, direct entries first.
    /// </summary>
    IReadOnlyList<IHandlerReference<IPostInterceptor>> PostInterceptors { get; }

    /// <summary>
    /// The exception interceptors to run when a stage throws, direct entries first.
    /// </summary>
    IReadOnlyList<IHandlerReference<IExceptionInterceptor>> ExceptionInterceptors { get; }

    /// <summary>
    /// The final interceptors to run once the pipeline settles, direct entries first.
    /// </summary>
    IReadOnlyList<IHandlerReference<IFinalInterceptor>> FinalInterceptors { get; }
}
