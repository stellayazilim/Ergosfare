using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Extensions;
using Stella.Ergosfare.Core.Internal.Abstractions;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Builders;

/// <summary>
/// Builds <see cref="PreInterceptorDescriptor"/> instances for types implementing <see cref="IPreInterceptor"/>.
/// </summary>
internal sealed class PreInterceptorDescriptionBuilder: IHandlerDescriptorBuilder
{
    
    /// <summary>
    /// Determines whether the specified type implements <see cref="IPreInterceptor"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the type implements <see cref="IPreInterceptor"/>; otherwise, <c>false</c>.</returns>
    public bool CanBuild([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        return type.IsAssignableTo(typeof(IPreInterceptor));
    }

    
    /// <summary>
    /// Builds one or more <see cref="PreInterceptorDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type from which to build descriptors.</param>
    /// <returns>A collection of <see cref="PreInterceptorDescriptor"/> instances representing the handler.</returns>
    public IEnumerable<IHandlerDescriptor> Build([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        // The sync and async contracts are independent hierarchies (no object-typed root
        // member ties them together), so both patterns are matched; a type implementing
        // both for the same message yields a single descriptor.
        var interfaces = handlerType.GetInterfacesEqualTo(typeof(IPreInterceptor<>))
            .Concat(handlerType.GetInterfacesEqualTo(typeof(IAsyncPreInterceptor<>)));
        var weight = handlerType.GetWeightFromAttribute();
        var seenMessageTypes = new HashSet<Type>();
        foreach (var @interface in interfaces)
        {
            var messageType = @interface.GetGenericArguments()[0];
            messageType = messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType;

            if (!seenMessageTypes.Add(messageType))
            {
                continue;
            }

            var groups = handlerType.GetGroupsFromAttribute();
            yield return new PreInterceptorDescriptor
            {
                Weight = weight,
                Groups = groups,
                MessageType = messageType,
                HandlerType = handlerType
            };
        }
    }
}