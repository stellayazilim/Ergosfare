using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

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
    
    public Type MessageType { get; } = messageType;
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
    public void AddDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {
        
        foreach (var descriptor in descriptors)
        {
            AddDescriptor(descriptor);
        }
    }
    
    
    
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
            }
        }
    }
    
    
}