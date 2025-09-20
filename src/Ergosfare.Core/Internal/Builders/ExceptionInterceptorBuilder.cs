
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Extensions;
using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Builders;

/// <summary>
/// Builds <see cref="ExceptionInterceptorDescriptor"/> instances for types implementing <see cref="IExceptionInterceptor"/>.
/// </summary>
public class ExceptionInterceptorDescriptorBuilder: IHandlerDescriptorBuilder
{
    /// <summary>
    /// Determines whether the specified type implements <see cref="IExceptionInterceptor"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the type implements <see cref="IExceptionInterceptor"/>; otherwise, <c>false</c>.</returns>
    public bool CanBuild(Type type)
    {
        return type.IsAssignableTo(typeof(IExceptionInterceptor));
    }

    /// <summary>
    /// Builds one or more <see cref="ExceptionInterceptorDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type from which to build descriptors.</param>
    /// <returns>A collection of <see cref="ExceptionInterceptorDescriptor"/> instances representing the handler.</returns>
    public IEnumerable<IHandlerDescriptor> Build(Type handlerType)
    {
        var interfaces = handlerType.GetInterfacesEqualTo(typeof( IExceptionInterceptor<,>));
        var weight = handlerType.GetWeightFromAttribute();
        foreach (var @interface in interfaces)
        {
            var messageType = @interface.GetGenericArguments()[0];
            var resultType = @interface.GetGenericArguments()[1];
            var groups = handlerType.GetGroupsFromAttribute();
            yield return new ExceptionInterceptorDescriptor
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