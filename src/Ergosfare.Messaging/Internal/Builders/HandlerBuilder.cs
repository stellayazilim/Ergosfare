using Ergosfare.Messaging.Abstractions;
using Ergosfare.Messaging.Abstractions.Registry.Descriptors;
using Ergosfare.Messaging.Extensions;
using Ergosfare.Messaging.Internal.Abstractions;
using Ergosfare.Messaging.Internal.Registry.Descriptors;

namespace Ergosfare.Messaging.Internal.Builders;

public sealed class HandlerDescriptorBuilder: IHandlerDescriptorBuilder
{
    public bool CanBuild(Type type)
    {
        return type.IsAssignableTo(typeof(IHandler));
    }

    public IHandlerDescriptor Build(Type handlerType)
    {
        var interfaces = handlerType.GetInterfacesEqualTo(typeof(IHandler<,>));

        var types = interfaces.ToList().First();
       
        var messageType = types.GetGenericArguments()[0];
        var resultType = types.GetGenericArguments()[1];
        
        return new MainHandlerDescriptor
        {
            MessageType = messageType,
            ResultType = resultType,
            HandlerType = handlerType,
        };
    }
}