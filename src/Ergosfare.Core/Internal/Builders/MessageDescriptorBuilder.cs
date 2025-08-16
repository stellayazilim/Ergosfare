using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Builders;

internal class MessageDescriptorBuilder
    (Type messageType)
{

    // private List<IMainHandlerDescriptor> _mainHandlers = new ();
    // private Type _messageType = messageType;
    //
    // public MessageDescriptorBuilder AddDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    // {
    //     foreach (var descriptor in descriptors)
    //     {
    //         AddDescriptor(descriptor);
    //     }
    //     return this;
    // }
    //
    //
    
    // public MessageDescriptorBuilder AddDescriptor(IHandlerDescriptor descriptor)
    // {
    //     if (messageType == descriptor.MessageType)
    //     {
    //         switch (descriptor)
    //         {
    //             case IMainHandlerDescriptor main : _messageDescriptor.Handlers.Add(main); break;
    //         }
    //     }
    //     
    //     else if (messageType.IsAssignableTo(descriptor.MessageType))
    //     {
    //         // no indrict handler atm
    //     }
    // }
 
}