using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal class MessageDescriptor(Type messageType) : IMessageDescriptor
{



    private readonly List<IMainHandlerDescriptor> _handlers = new();
    private readonly List<IMainHandlerDescriptor> _indirectHandlers = new();
    
    public Type MessageType { get; } = messageType;
    public bool IsGeneric { get; } = messageType.IsGenericType;

    public IReadOnlyCollection<IMainHandlerDescriptor> Handlers => _handlers;
    public IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers => _indirectHandlers;

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
                case IMainHandlerDescriptor mainHandlerDescriptor : 
                    _handlers.Add(mainHandlerDescriptor); 
                    break;
            }
        }
        
        else if (MessageType.IsAssignableTo(descriptor.MessageType))
        {
            switch (descriptor)
            {
          
                case IMainHandlerDescriptor mainHandlerDescriptor:
                    _indirectHandlers.Add(mainHandlerDescriptor);
                    break;
             
            }
        }
    }
    
    
}