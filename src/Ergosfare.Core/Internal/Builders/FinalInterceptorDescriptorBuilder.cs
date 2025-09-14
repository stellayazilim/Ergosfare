using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Extensions;
using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Builders;

public class FinalInterceptorDescriptorBuilder: IHandlerDescriptorBuilder
{
    public bool CanBuild(Type type)
    {
        return type.IsAssignableTo(typeof(IFinalInterceptor));
    }

    public IEnumerable<IHandlerDescriptor> Build(Type handlerType)
    {
        var interfaces = handlerType.GetInterfacesEqualTo(typeof( IFinalInterceptor<,>));
        var weight = handlerType.GetWeightFromAttribute();
        foreach (var @interface in interfaces)
        {
            var messageType = @interface.GetGenericArguments()[0];
            var resultType = @interface.GetGenericArguments()[1];
            var groups = handlerType.GetGroupsFromAttribute();
            yield return new FinalInterceptorDescriptor()
            {
                Weight = weight,
                Groups = groups,
                MessageType = messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType,
                ResultType = resultType,
                HandlerType = handlerType
            };
        }
    }
}