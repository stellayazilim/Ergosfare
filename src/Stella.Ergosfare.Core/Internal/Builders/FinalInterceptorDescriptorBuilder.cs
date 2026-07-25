using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Extensions;
using Stella.Ergosfare.Core.Internal.Abstractions;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Builders;



/// <summary>
/// A descriptor builder responsible for creating <see cref="FinalInterceptorDescriptor"/> instances.
/// Handles types that implement <see cref="IFinalInterceptor"/> interfaces,
/// extracting metadata such as weight, groups, message type, and result type.
/// </summary>
internal sealed class FinalInterceptorDescriptorBuilder: IHandlerDescriptorBuilder
{
    
    /// <summary>
    /// Determines whether this builder can construct descriptors for the given type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>
    /// <c>true</c> if the type implements <see cref="IFinalInterceptor"/>; otherwise <c>false</c>.
    /// </returns>
    public bool CanBuild([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        return type.IsAssignableTo(typeof(IFinalInterceptor));
    }

    /// <summary>
    /// Builds one or more <see cref="FinalInterceptorDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type that implements <see cref="IFinalInterceptor{TMessage, TResult}"/>.</param>
    /// <returns>
    /// A sequence of <see cref="FinalInterceptorDescriptor"/> objects describing the handler,
    /// including its weight, groups, message type, result type, and handler type.
    /// </returns>
    public IEnumerable<IHandlerDescriptor> Build([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        // The sync and async contracts are independent hierarchies (no object-typed root
        // member ties them together), so all three patterns are matched; the result-agnostic
        // async contract maps to a ResultType of object, exactly what its former sync base
        // interface carried. Duplicate (message, result) pairs yield a single descriptor.
        var interfaces = handlerType.GetInterfacesEqualTo(typeof(IFinalInterceptor<,>))
            .Concat(handlerType.GetInterfacesEqualTo(typeof(IAsyncFinalInterceptor<,>)))
            .Concat(handlerType.GetInterfacesEqualTo(typeof(IAsyncFinalInterceptor<>)));

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

            yield return new FinalInterceptorDescriptor()
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