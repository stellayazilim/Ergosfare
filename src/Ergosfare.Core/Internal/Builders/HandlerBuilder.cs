using Ergosfare.Contracts;
using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Extensions;

namespace Ergosfare.Core.Internal.Builders;

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