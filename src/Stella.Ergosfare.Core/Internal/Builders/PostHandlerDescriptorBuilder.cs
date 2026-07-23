using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Extensions;
using Stella.Ergosfare.Core.Internal.Abstractions;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Builders;


/// <summary>
/// Builds <see cref="PostInterceptorDescriptor"/> instances for types implementing <see cref="IPostInterceptor"/>.
/// </summary>
internal sealed class PostHandlerDescriptorBuilder: IHandlerDescriptorBuilder
{
    /// <summary>
    /// Determines whether the specified type implements <see cref="IPostInterceptor"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the type implements <see cref="IPostInterceptor"/>; otherwise, <c>false</c>.</returns>
    public bool CanBuild([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        return type.IsAssignableTo(typeof(IPostInterceptor));
    }

    /// <summary>
    /// Builds one or more <see cref="PostInterceptorDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type from which to build descriptors.</param>
    /// <returns>A collection of <see cref="PostInterceptorDescriptor"/> instances representing the handler.</returns>
    public IEnumerable<IHandlerDescriptor> Build([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        // The sync and async contracts are independent hierarchies (no object-typed root
        // member ties them together), so all three patterns are matched; the result-agnostic
        // async contract maps to a ResultType of object, exactly what its former sync base
        // interface carried. Duplicate (message, result) pairs yield a single descriptor.
        var interfaces = handlerType.GetInterfacesEqualTo(typeof(IPostInterceptor<,>))
            .Concat(handlerType.GetInterfacesEqualTo(typeof(IAsyncPostInterceptor<,>)))
            .Concat(handlerType.GetInterfacesEqualTo(typeof(IAsyncPostInterceptor<>)));
        var weight = handlerType.GetWeightFromAttribute();
        var seenPairs = new HashSet<(Type MessageType, Type ResultType)>();
        foreach (var @interface in interfaces)
        {
            var genericArguments = @interface.GetGenericArguments();
            var messageType = genericArguments[0];
            messageType = messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType;
            var resultType = genericArguments.Length > 1 ? genericArguments[1] : typeof(object);

            if (!seenPairs.Add((messageType, resultType)))
            {
                continue;
            }

            var groups = handlerType.GetGroupsFromAttribute();
            yield return new PostInterceptorDescriptor
            {
                Weight = weight,
                Groups = groups,
                MessageType = messageType,
                ResultType = resultType,
                HandlerType = handlerType
            };
        }
    }
}