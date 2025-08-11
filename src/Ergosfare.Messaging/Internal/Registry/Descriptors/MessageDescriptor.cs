using System.Runtime.CompilerServices;
using Ergosfare.Messaging.Abstractions.Registry.Descriptors;


[assembly: InternalsVisibleTo("Ergosfare.Messaging.Test")]
namespace Ergosfare.Messaging.Internal.Registry.Descriptors;

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