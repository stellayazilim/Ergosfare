using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Extensions;
using Stella.Ergosfare.Core.Internal.Abstractions;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Builders;

/// <summary>
/// Builds <see cref="MainHandlerDescriptor"/> instances for types implementing <see cref="IHandler"/>.
/// </summary>
internal sealed class HandlerDescriptorBuilder: IHandlerDescriptorBuilder
{
    
    /// <summary>
    /// Determines whether the specified type implements <see cref="IHandler"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the type implements <see cref="IHandler"/>; otherwise, <c>false</c>.</returns>
    public bool CanBuild([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        return type.IsAssignableTo(typeof(IHandler));
    }

    
    /// <summary>
    /// Builds one or more <see cref="MainHandlerDescriptor"/> instances for the given handler type.
    /// </summary>
    /// <param name="handlerType">The handler type from which to build descriptors.</param>
    /// <returns>A collection of <see cref="MainHandlerDescriptor"/> instances representing the handler.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "ValueTask<TResult> descriptor result types are metadata only; the pipeline never instantiates them reflectively.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "ValueTask<TResult> descriptor result types are metadata only; the pipeline never instantiates them reflectively.")]
    public IEnumerable<IHandlerDescriptor> Build([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        var weight = handlerType.GetWeightFromAttribute();

        // Synchronous handlers: the second argument IS the result type.
        foreach (var @interface in handlerType.GetInterfacesEqualTo(typeof(IHandler<,>)))
        {
            yield return new MainHandlerDescriptor
            {
                Weight = weight,
                Groups = handlerType.GetGroupsFromAttribute(),
                MessageType = @interface.GetGenericArguments()[0],
                ResultType = @interface.GetGenericArguments()[1],
                HandlerType = handlerType,
            };
        }

        // Asynchronous handlers are standalone contracts (no object-typed root); their
        // descriptors record the ValueTask carrier as the result type for parity with the
        // pre-severance descriptor shape.
        foreach (var @interface in handlerType.GetInterfacesEqualTo(typeof(IAsyncHandler<>)))
        {
            yield return new MainHandlerDescriptor
            {
                Weight = weight,
                Groups = handlerType.GetGroupsFromAttribute(),
                MessageType = @interface.GetGenericArguments()[0],
                ResultType = typeof(ValueTask),
                HandlerType = handlerType,
            };
        }

        foreach (var @interface in handlerType.GetInterfacesEqualTo(typeof(IAsyncHandler<,>)))
        {
            yield return new MainHandlerDescriptor
            {
                Weight = weight,
                Groups = handlerType.GetGroupsFromAttribute(),
                MessageType = @interface.GetGenericArguments()[0],
                ResultType = typeof(ValueTask<>).MakeGenericType(@interface.GetGenericArguments()[1]),
                HandlerType = handlerType,
            };
        }
    }
}