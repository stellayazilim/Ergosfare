using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal class MessageDescriptor : IMessageDescriptor
{
    public MessageDescriptor(Type messageType)
    {
        MessageType = messageType;
        IsGeneric = messageType.IsGenericType;
    }

    public Type MessageType { get; }
    public bool IsGeneric { get; }
    public IMainHandlerDescriptor Handler { get; private set; }
    
    
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
             
                case IMainHandlerDescriptor mainHandlerDescriptor:
                    Handler = mainHandlerDescriptor;
            
                    break;
            }
        }
    }
}