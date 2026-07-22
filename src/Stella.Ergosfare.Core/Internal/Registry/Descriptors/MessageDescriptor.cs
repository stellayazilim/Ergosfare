using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Represents a descriptor for a specific message type, holding all associated handlers
/// and interceptors (pre, main, post, exception, and final).
/// </summary>
/// <remarks>
/// This class categorizes descriptors into direct and indirect collections:
/// - Direct descriptors match the message type exactly.
/// - Indirect descriptors match assignable message types (base types or interfaces).
/// The backing lists are created lazily on first add — most messages use only one or
/// two of the ten collections, so empty stages share <see cref="Array.Empty{T}"/>.
/// </remarks>
internal class MessageDescriptor(Type messageType) : IMessageDescriptor
{

    // pre interceptors
    private List<IPreInterceptorDescriptor>? _preInterceptors;
    private List<IPreInterceptorDescriptor>? _indirectPreInterceptors;

    // main handlers
    private List<IMainHandlerDescriptor>? _handlers;
    private List<IMainHandlerDescriptor>? _indirectHandlers;

    // post interceptors
    private List<IPostInterceptorDescriptor>? _postInterceptors;
    private List<IPostInterceptorDescriptor>? _indirectPostInterceptors;

    // exception interceptors
    private List<IExceptionInterceptorDescriptor>? _exceptionInterceptors;
    private List<IExceptionInterceptorDescriptor>? _indirectExceptionInterceptors;

    // final interceptors
    private List<IFinalInterceptorDescriptor>? _finalInterceptors;
    private List<IFinalInterceptorDescriptor>? _indirectFinalInterceptors;


    /// <summary>
    /// Gets the message type associated with this descriptor.
    /// </summary>
    public Type MessageType { get; } = messageType;

    /// <summary>
    /// Gets a value indicating whether the message type is generic.
    /// </summary>
    public bool IsGeneric { get; } = messageType.IsGenericType;

    // pre interceptor
    public IReadOnlyCollection<IPreInterceptorDescriptor> PreInterceptors => _preInterceptors ?? (IReadOnlyCollection<IPreInterceptorDescriptor>)Array.Empty<IPreInterceptorDescriptor>();
    public IReadOnlyCollection<IPreInterceptorDescriptor> IndirectPreInterceptors => _indirectPreInterceptors ?? (IReadOnlyCollection<IPreInterceptorDescriptor>)Array.Empty<IPreInterceptorDescriptor>();

    // main handlers
    public IReadOnlyCollection<IMainHandlerDescriptor> Handlers => _handlers ?? (IReadOnlyCollection<IMainHandlerDescriptor>)Array.Empty<IMainHandlerDescriptor>();
    public IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers => _indirectHandlers ?? (IReadOnlyCollection<IMainHandlerDescriptor>)Array.Empty<IMainHandlerDescriptor>();

    // post interceptors
    public IReadOnlyCollection<IPostInterceptorDescriptor> PostInterceptors => _postInterceptors ?? (IReadOnlyCollection<IPostInterceptorDescriptor>)Array.Empty<IPostInterceptorDescriptor>();
    public IReadOnlyCollection<IPostInterceptorDescriptor> IndirectPostInterceptors => _indirectPostInterceptors ?? (IReadOnlyCollection<IPostInterceptorDescriptor>)Array.Empty<IPostInterceptorDescriptor>();


    // exception interceptors
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> ExceptionInterceptors => _exceptionInterceptors ?? (IReadOnlyCollection<IExceptionInterceptorDescriptor>)Array.Empty<IExceptionInterceptorDescriptor>();
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> IndirectExceptionInterceptors => _indirectExceptionInterceptors ?? (IReadOnlyCollection<IExceptionInterceptorDescriptor>)Array.Empty<IExceptionInterceptorDescriptor>();

    // final interceptors
    public IReadOnlyCollection<IFinalInterceptorDescriptor> FinalInterceptors => _finalInterceptors ?? (IReadOnlyCollection<IFinalInterceptorDescriptor>)Array.Empty<IFinalInterceptorDescriptor>();
    public IReadOnlyCollection<IFinalInterceptorDescriptor> IndirectFinalInterceptors => _indirectFinalInterceptors ?? (IReadOnlyCollection<IFinalInterceptorDescriptor>)Array.Empty<IFinalInterceptorDescriptor>();

    /// <summary>
    /// Adds multiple handler descriptors to this message descriptor.
    /// </summary>
    /// <param name="descriptors">The collection of descriptors to add.</param>
    public void AddDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {

        foreach (var descriptor in descriptors)
        {
            AddDescriptor(descriptor);
        }
    }


    /// <summary>
    /// Adds a single handler descriptor to this message descriptor.
    /// Direct or indirect placement depends on whether the descriptor's
    /// <see cref="IHandlerDescriptor.MessageType"/> exactly matches or is assignable to the message type.
    /// </summary>
    /// <param name="descriptor">The descriptor to add.</param>
    public void AddDescriptor(IHandlerDescriptor descriptor)
    {
        if (MessageType == descriptor.MessageType)
        {
            switch (descriptor)
            {
                case IPreInterceptorDescriptor preInterceptorDescriptor:
                    (_preInterceptors ??= new()).Add(preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor :
                    (_handlers ??= new()).Add(mainHandlerDescriptor);
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    (_postInterceptors ??= new()).Add(postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    (_exceptionInterceptors ??= new()).Add(exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    (_finalInterceptors ??= new()).Add(finalInterceptorDescriptor);
                    break;
            }
        }

        else if (MessageType.IsAssignableTo(descriptor.MessageType))
        {
            switch (descriptor)
            {
                case IPreInterceptorDescriptor preInterceptorDescriptor:
                    (_indirectPreInterceptors ??= new()).Add(preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor:
                    (_indirectHandlers ??= new()).Add(mainHandlerDescriptor);
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    (_indirectPostInterceptors ??= new()).Add(postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    (_indirectExceptionInterceptors ??= new()).Add(exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    (_indirectFinalInterceptors ??= new()).Add(finalInterceptorDescriptor);
                    break;
            }
        }
    }
}
