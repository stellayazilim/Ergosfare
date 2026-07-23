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
/// Each stage is an immutable array replaced wholesale on mutation (copy-on-write):
/// registration-time writers (serialized by the registry's gate) swap in a new array,
/// so dispatch-time readers building pipeline shapes always observe a consistent stage —
/// never a list mid-resize. Empty stages share <see cref="Array.Empty{T}"/> via the
/// null-backed properties; most messages use only one or two of the ten collections.
/// </remarks>
internal class MessageDescriptor(Type messageType) : IMessageDescriptor
{

    // pre interceptors
    private IPreInterceptorDescriptor[]? _preInterceptors;
    private IPreInterceptorDescriptor[]? _indirectPreInterceptors;

    // main handlers
    private IMainHandlerDescriptor[]? _handlers;
    private IMainHandlerDescriptor[]? _indirectHandlers;

    // post interceptors
    private IPostInterceptorDescriptor[]? _postInterceptors;
    private IPostInterceptorDescriptor[]? _indirectPostInterceptors;

    // exception interceptors
    private IExceptionInterceptorDescriptor[]? _exceptionInterceptors;
    private IExceptionInterceptorDescriptor[]? _indirectExceptionInterceptors;

    // final interceptors
    private IFinalInterceptorDescriptor[]? _finalInterceptors;
    private IFinalInterceptorDescriptor[]? _indirectFinalInterceptors;


    /// <summary>
    /// Gets the message type associated with this descriptor.
    /// </summary>
    public Type MessageType { get; } = messageType;

    /// <summary>
    /// Gets a value indicating whether the message type is generic.
    /// </summary>
    public bool IsGeneric { get; } = messageType.IsGenericType;

    // pre interceptor
    public IReadOnlyCollection<IPreInterceptorDescriptor> PreInterceptors => _preInterceptors ?? Array.Empty<IPreInterceptorDescriptor>();
    public IReadOnlyCollection<IPreInterceptorDescriptor> IndirectPreInterceptors => _indirectPreInterceptors ?? Array.Empty<IPreInterceptorDescriptor>();

    // main handlers
    public IReadOnlyCollection<IMainHandlerDescriptor> Handlers => _handlers ?? Array.Empty<IMainHandlerDescriptor>();
    public IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers => _indirectHandlers ?? Array.Empty<IMainHandlerDescriptor>();

    // post interceptors
    public IReadOnlyCollection<IPostInterceptorDescriptor> PostInterceptors => _postInterceptors ?? Array.Empty<IPostInterceptorDescriptor>();
    public IReadOnlyCollection<IPostInterceptorDescriptor> IndirectPostInterceptors => _indirectPostInterceptors ?? Array.Empty<IPostInterceptorDescriptor>();


    // exception interceptors
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> ExceptionInterceptors => _exceptionInterceptors ?? Array.Empty<IExceptionInterceptorDescriptor>();
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> IndirectExceptionInterceptors => _indirectExceptionInterceptors ?? Array.Empty<IExceptionInterceptorDescriptor>();

    // final interceptors
    public IReadOnlyCollection<IFinalInterceptorDescriptor> FinalInterceptors => _finalInterceptors ?? Array.Empty<IFinalInterceptorDescriptor>();
    public IReadOnlyCollection<IFinalInterceptorDescriptor> IndirectFinalInterceptors => _indirectFinalInterceptors ?? Array.Empty<IFinalInterceptorDescriptor>();

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
                    Append(ref _preInterceptors, preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor :
                    Append(ref _handlers, mainHandlerDescriptor);
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    Append(ref _postInterceptors, postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    Append(ref _exceptionInterceptors, exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    Append(ref _finalInterceptors, finalInterceptorDescriptor);
                    break;
            }
        }

        else if (MessageType.IsAssignableTo(descriptor.MessageType))
        {
            switch (descriptor)
            {
                case IPreInterceptorDescriptor preInterceptorDescriptor:
                    Append(ref _indirectPreInterceptors, preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor:
                    Append(ref _indirectHandlers, mainHandlerDescriptor);
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    Append(ref _indirectPostInterceptors, postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    Append(ref _indirectExceptionInterceptors, exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    Append(ref _indirectFinalInterceptors, finalInterceptorDescriptor);
                    break;
            }
        }
    }

    /// <summary>
    /// Appends an item to a stage by swapping in a newly built array. The reference
    /// assignment is atomic, so a concurrent reader observes either the previous or the
    /// new stage — never a partially built one. Registration is serialized by the
    /// registry's gate, so writers never race each other here.
    /// </summary>
    private static void Append<T>(ref T[]? stage, T item)
    {
        if (stage is null)
        {
            stage = [item];
            return;
        }

        var next = new T[stage.Length + 1];
        Array.Copy(stage, next, stage.Length);
        next[^1] = item;
        stage = next;
    }
}
