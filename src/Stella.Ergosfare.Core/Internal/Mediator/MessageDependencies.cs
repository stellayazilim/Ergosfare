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
        Handlers = Materialize<IHandler, IMainHandlerDescriptor>(shape.Handlers, memoizedProvider);
        IndirectHandlers = Materialize<IHandler, IMainHandlerDescriptor>(shape.IndirectHandlers, memoizedProvider);
        PreInterceptors = Materialize<IPreInterceptor, IPreInterceptorDescriptor>(shape.PreInterceptors, memoizedProvider);
        PostInterceptors = Materialize<IPostInterceptor, IPostInterceptorDescriptor>(shape.PostInterceptors, memoizedProvider);
        ExceptionInterceptors = Materialize<IExceptionInterceptor, IExceptionInterceptorDescriptor>(shape.ExceptionInterceptors, memoizedProvider);
        FinalInterceptors = Materialize<IFinalInterceptor, IFinalInterceptorDescriptor>(shape.FinalInterceptors, memoizedProvider);
    }

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
