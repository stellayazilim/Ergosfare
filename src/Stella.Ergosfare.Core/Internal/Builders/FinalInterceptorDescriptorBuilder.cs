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
public class FinalInterceptorDescriptorBuilder: IHandlerDescriptorBuilder
{
    
    /// <summary>
    /// Determines whether this builder can construct descriptors for the given type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>
    /// <c>true</c> if the type implements <see cref="IFinalInterceptor"/>; otherwise <c>false</c>.
    /// </returns>
    public bool CanBuild(Type type)
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
    public IEnumerable<IHandlerDescriptor> Build(Type handlerType)
    {
        // Identify the interfaces on the handler that match IFinalInterceptor<TMessage, TResult>
        var interfaces = handlerType.GetInterfacesEqualTo(typeof( IFinalInterceptor<,>));
        
        // Get weight from [Weight] attribute if present
        var weight = handlerType.GetWeightFromAttribute();
        foreach (var @interface in interfaces)
        {
            var messageType = @interface.GetGenericArguments()[0];
            var resultType = @interface.GetGenericArguments()[1];
            
            // Extract group metadata from [Group] attribute(s)
            var groups = handlerType.GetGroupsFromAttribute();
            
            // Construct and yield the descriptor
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