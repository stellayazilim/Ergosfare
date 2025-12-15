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
/// </remarks>
internal class MessageDescriptor(Type messageType) : IMessageDescriptor
{

    // pre interceptors
    private readonly List<IPreInterceptorDescriptor> _preInterceptors = new();
    private readonly List<IPreInterceptorDescriptor> _indirectPreInterceptors = new();

    // main handlers
    private readonly List<IMainHandlerDescriptor> _handlers = new();
    private readonly List<IMainHandlerDescriptor> _indirectHandlers = new();
    
    // post interceptors
    private readonly List<IPostInterceptorDescriptor> _postInterceptors = new();
    private readonly List<IPostInterceptorDescriptor> _indirectPostInterceptors = new();
    
    // exception interceptors
    private readonly List<IExceptionInterceptorDescriptor> _exceptionInterceptors = new();
    private readonly List<IExceptionInterceptorDescriptor> _indirectExceptionInterceptors = new();
    
    // final intercepors
    private readonly List<IFinalInterceptorDescriptor> _finalInterceptors = new();
    private readonly List<IFinalInterceptorDescriptor> _indirectFinalInterceptors = new();
    
    
    /// <summary>
    /// Gets the message type associated with this descriptor.
    /// </summary>
    public Type MessageType { get; } = messageType;
    
    /// <summary>
    /// Gets a value indicating whether the message type is generic.
    /// </summary>
    public bool IsGeneric { get; } = messageType.IsGenericType;

    // pre interceptor
    public IReadOnlyCollection<IPreInterceptorDescriptor> PreInterceptors => _preInterceptors;
    public IReadOnlyCollection<IPreInterceptorDescriptor> IndirectPreInterceptors => _indirectPreInterceptors;
    
    // main handlers
    public IReadOnlyCollection<IMainHandlerDescriptor> Handlers => _handlers;
    public IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers => _indirectHandlers;

    // post interceptors
    public IReadOnlyCollection<IPostInterceptorDescriptor> PostInterceptors => _postInterceptors;
    public IReadOnlyCollection<IPostInterceptorDescriptor> IndirectPostInterceptors => _indirectPostInterceptors;
    
    
    // exception interceptors
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> ExceptionInterceptors => _exceptionInterceptors;
    public IReadOnlyCollection<IExceptionInterceptorDescriptor> IndirectExceptionInterceptors => _indirectExceptionInterceptors;
    
    // filan interceptors
    public IReadOnlyCollection<IFinalInterceptorDescriptor> FinalInterceptors => _finalInterceptors;
    public IReadOnlyCollection<IFinalInterceptorDescriptor> IndirectFinalInterceptors => _indirectFinalInterceptors;
    
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
                    _preInterceptors.Add(preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor : 
                    _handlers.Add(mainHandlerDescriptor); 
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    _postInterceptors.Add(postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    _exceptionInterceptors.Add(exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    _finalInterceptors.Add(finalInterceptorDescriptor);
                    break;
            }
        }
        
        else if (MessageType.IsAssignableTo(descriptor.MessageType))
        {
            switch (descriptor)
            {
                case IPreInterceptorDescriptor preInterceptorDescriptor:
                    _indirectPreInterceptors.Add(preInterceptorDescriptor);
                    break;
                case IMainHandlerDescriptor mainHandlerDescriptor:
                    _indirectHandlers.Add(mainHandlerDescriptor);
                    break;
                case IPostInterceptorDescriptor postInterceptorDescriptor:
                    _indirectPostInterceptors.Add(postInterceptorDescriptor);
                    break;
                case IExceptionInterceptorDescriptor exceptionInterceptorDescriptor:
                    _indirectExceptionInterceptors.Add(exceptionInterceptorDescriptor);
                    break;
                case IFinalInterceptorDescriptor finalInterceptorDescriptor:
                    _indirectFinalInterceptors.Add(finalInterceptorDescriptor);
                    break;
            }
        }
    }
}