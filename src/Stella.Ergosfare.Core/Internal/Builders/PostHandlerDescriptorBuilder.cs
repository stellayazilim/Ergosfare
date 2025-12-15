using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Extensions;
using Stella.Ergosfare.Core.Internal.Abstractions;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Builders;


/// <summary>
/// Builds <see cref="PostInterceptorDescriptor"/> instances for types implementing <see cref="IPostInterceptor"/>.
/// </summary>
public sealed class PostHandlerDescriptorBuilder: IHandlerDescriptorBuilder
{
    /// <summary>
    /// Determines whether the specified type implements <see cref="IPostInterceptor"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the type implements <see cref="IPostInterceptor"/>; otherwise, <c>false</c>.</returns>
    public bool CanBuild(Type type)
    {
        return type.IsAssignableTo(typeof(IPostInterceptor));
    }

    /// <summary>
    /// Builds one or more <see cref="PostInterceptorDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type from which to build descriptors.</param>
    /// <returns>A collection of <see cref="PostInterceptorDescriptor"/> instances representing the handler.</returns>
    public IEnumerable<IHandlerDescriptor> Build(Type handlerType)
    {
        var interfaces = handlerType.GetInterfacesEqualTo(typeof(IPostInterceptor<,>));
        var weight = handlerType.GetWeightFromAttribute();
        foreach (var @interface in interfaces)
        {
            var messageType = @interface.GetGenericArguments()[0];
            var resultType = @interface.GetGenericArguments()[1];
            var groups = handlerType.GetGroupsFromAttribute();
            yield return new PostInterceptorDescriptor
            {
                Weight = weight,
                Groups = groups,
                MessageType = messageType.IsGenericType   ? messageType.GetGenericTypeDefinition() : messageType,
                ResultType = resultType,
                HandlerType = handlerType
            };
        }
    }
}