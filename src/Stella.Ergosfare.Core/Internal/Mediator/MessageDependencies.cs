using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// Provides lazy resolution and access to all handler dependencies for a given message type,
/// including pre/post interceptors, main handlers, exception interceptors, and final interceptors.
/// </summary>
/// <remarks>
/// This class resolves handlers using an <see cref="IServiceProvider"/>.
/// All type resolution work lives in the cached <see cref="MessagePipelineShape"/>: group
/// filtering, ordering, and the concrete (closed) handler type per descriptor. This class
/// only binds the shape's planned handlers to a provider through lazy wrappers, so
/// per-scope construction stays cheap. Handlers are returned as lazy collections to
/// avoid unnecessary instantiation until they are required for mediation.
/// </remarks>
internal sealed class MessageDependencies : IMessageDependencies
{

    /// <summary>
    /// Gets the lazy collection of pre-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of indirect pre-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of main handlers for the message.
    /// </summary>
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }

    /// <summary>
    /// Gets the lazy collection of indirect main handlers for the message.
    /// </summary>
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }

    /// <summary>
    /// Gets the lazy collection of post-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of indirect post-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of exception interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of indirect exception interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of final interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> FinalInterceptors { get; }

    /// <summary>
    /// Gets the lazy collection of indirect final interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> IndirectFinalInterceptors { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MessageDependencies"/> for the given message type,
    /// descriptor, service provider, and groups. Builds a pipeline shape on the fly; prefer the
    /// shape-based constructor with a cached shape on hot paths.
    /// </summary>
    /// <param name="messageType">The type of the message for which dependencies are resolved.</param>
    /// <param name="descriptor">The message descriptor providing handler metadata.</param>
    /// <param name="serviceProvider">The service provider used to resolve handler instances.</param>
    /// <param name="groups">The groups to filter handlers by; if none provided, the default group is used.</param>
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
        : this(MessagePipelineShape.Create(messageType, descriptor, groups), serviceProvider)
    {
    }

    /// <summary>
    /// Initializes a new instance from a (cached) pipeline shape, materializing only the
    /// lazy handler wrappers bound to the given provider.
    /// </summary>
    /// <param name="shape">The ordered, group-filtered pipeline shape with pre-resolved handler types.</param>
    /// <param name="serviceProvider">The service provider used to resolve handler instances.</param>
    public MessageDependencies(MessagePipelineShape shape, IServiceProvider serviceProvider)
    {
        Handlers = Materialize<IHandler, IMainHandlerDescriptor>(shape.Handlers, serviceProvider);
        IndirectHandlers = Materialize<IHandler, IMainHandlerDescriptor>(shape.IndirectHandlers, serviceProvider);
        PreInterceptors = Materialize<IPreInterceptor, IPreInterceptorDescriptor>(shape.PreInterceptors, serviceProvider);
        IndirectPreInterceptors = Materialize<IPreInterceptor, IPreInterceptorDescriptor>(shape.IndirectPreInterceptors, serviceProvider);
        PostInterceptors = Materialize<IPostInterceptor, IPostInterceptorDescriptor>(shape.PostInterceptors, serviceProvider);
        IndirectPostInterceptors = Materialize<IPostInterceptor, IPostInterceptorDescriptor>(shape.IndirectPostInterceptors, serviceProvider);
        ExceptionInterceptors = Materialize<IExceptionInterceptor, IExceptionInterceptorDescriptor>(shape.ExceptionInterceptors, serviceProvider);
        IndirectExceptionInterceptors = Materialize<IExceptionInterceptor, IExceptionInterceptorDescriptor>(shape.IndirectExceptionInterceptors, serviceProvider);
        FinalInterceptors = Materialize<IFinalInterceptor, IFinalInterceptorDescriptor>(shape.FinalInterceptors, serviceProvider);
        IndirectFinalInterceptors = Materialize<IFinalInterceptor, IFinalInterceptorDescriptor>(shape.IndirectFinalInterceptors, serviceProvider);
    }


    /// <summary>
    /// Shared empty collection per closed generic, so empty pipeline stages cost nothing
    /// to build — important when dependencies are constructed per scope.
    /// </summary>
    private static class EmptyLazyHandlers<THandler, TDescriptor> where TDescriptor : IHandlerDescriptor
    {
        public static readonly ILazyHandlerCollection<THandler, TDescriptor> Instance =
            new LazyHandlerCollection<THandler, TDescriptor>([]);
    }

    /// <summary>
    /// Wraps the shape's planned handlers in lazily-resolving entries bound to the given
    /// provider. The handler type comes pre-resolved from the shape, so resolution is a
    /// plain service lookup.
    /// </summary>
    private static ILazyHandlerCollection<THandler, TDescriptor> Materialize<THandler, TDescriptor>(
        PlannedHandler<TDescriptor>[] plannedHandlers,
        IServiceProvider serviceProvider) where TDescriptor : IHandlerDescriptor
    {
        if (plannedHandlers.Length == 0)
        {
            return EmptyLazyHandlers<THandler, TDescriptor>.Instance;
        }

        var lazyHandlers = new ILazyHandler<THandler, TDescriptor>[plannedHandlers.Length];

        for (var i = 0; i < plannedHandlers.Length; i++)
        {
            var handlerType = plannedHandlers[i].HandlerType;
            lazyHandlers[i] = new LazyHandler<THandler, TDescriptor>
            {
                Handler = new Lazy<THandler>(() => (THandler) serviceProvider.GetRequiredService(handlerType)),
                Descriptor = plannedHandlers[i].Descriptor
            };
        }

        return new LazyHandlerCollection<THandler, TDescriptor>(lazyHandlers);
    }
}
