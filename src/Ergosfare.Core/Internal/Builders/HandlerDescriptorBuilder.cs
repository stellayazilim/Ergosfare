using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Extensions;

namespace Ergosfare.Core.Internal.Builders;

/// <summary>
/// Builds <see cref="MainHandlerDescriptor"/> instances for types implementing <see cref="IHandler"/>.
/// </summary>
public sealed class HandlerDescriptorBuilder: IHandlerDescriptorBuilder
{
    
    /// <summary>
    /// Determines whether the specified type implements <see cref="IHandler"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the type implements <see cref="IHandler"/>; otherwise, <c>false</c>.</returns>
    public bool CanBuild(Type type)
    {
        return type.IsAssignableTo(typeof(IHandler));
    }

    
    /// <summary>
    /// Builds one or more <see cref="MainHandlerDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type from which to build descriptors.</param>
    /// <returns>A collection of <see cref="MainHandlerDescriptor"/> instances representing the handler.</returns>
    public IEnumerable<IHandlerDescriptor> Build(Type handlerType)
    {
        var interfaces = handlerType.GetInterfacesEqualTo(typeof(IHandler<,>));

        var weight = handlerType.GetWeightFromAttribute();
        
        foreach (var @interface in interfaces)
        {
            var messageType = @interface.GetGenericArguments()[0];
            var resultType = @interface.GetGenericArguments()[1];
            var groups = handlerType.GetGroupsFromAttribute();
            yield return new MainHandlerDescriptor
            {
                Weight = weight,
                Groups = groups,
                MessageType = messageType,
                ResultType = resultType,
                HandlerType = handlerType,
            };
        }
    }
}