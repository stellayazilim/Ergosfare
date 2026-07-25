using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

/// <summary>
/// Factory for pre-built handler descriptors — the construction surface behind
/// <see cref="Stella.Ergosfare.Core.Abstractions.Registry.IMessageRegistry.RegisterDescriptors"/>.
/// Source-generated registration code creates descriptors through these methods with
/// statically known types, bypassing the reflection-based descriptor builders entirely;
/// the values mirror exactly what those builders would have computed for the same handler
/// type (verbatim message types for main handlers, generic-definition-normalized message
/// types for interceptors, <see cref="System.Threading.Tasks.ValueTask"/>-carrier result
/// types for asynchronous handlers).
/// </summary>
public static class HandlerDescriptors
{
    /// <summary>
    /// The group set applied when a handler declares no <see cref="GroupAttribute"/> —
    /// identical to the reflection path's fallback.
    /// </summary>
    private static readonly string[] DefaultGroups = [GroupAttribute.DefaultGroupName];

    /// <summary>Creates a main-handler descriptor.</summary>
    /// <param name="messageType">The handled message type, exactly as declared on the handler contract.</param>
    /// <param name="resultType">
    /// The pipeline result carrier: the declared <c>TResult</c> for synchronous contracts,
    /// <see cref="System.Threading.Tasks.ValueTask"/> for result-less asynchronous contracts,
    /// or <see cref="System.Threading.Tasks.ValueTask{TResult}"/> for result-producing ones.
    /// </param>
    /// <param name="handlerType">The concrete handler type to resolve and invoke.</param>
    /// <param name="weight">The execution-order weight (higher runs earlier); 0 when undeclared.</param>
    /// <param name="groups">The handler's group names, or <c>null</c> for the default group.</param>
    public static IMainHandlerDescriptor Handler(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight = 0,
        IReadOnlyCollection<string>? groups = null)
        => new GeneratedMainHandlerDescriptor(messageType, resultType, handlerType, weight, groups ?? DefaultGroups);

    /// <summary>Creates a pre-interceptor descriptor.</summary>
    /// <param name="messageType">The intercepted message type (generic definitions for generic messages).</param>
    /// <param name="handlerType">The concrete interceptor type to resolve and invoke.</param>
    /// <param name="weight">The execution-order weight (higher runs earlier); 0 when undeclared.</param>
    /// <param name="groups">The interceptor's group names, or <c>null</c> for the default group.</param>
    public static IPreInterceptorDescriptor PreInterceptor(
        Type messageType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight = 0,
        IReadOnlyCollection<string>? groups = null)
        => new GeneratedPreInterceptorDescriptor(messageType, handlerType, weight, groups ?? DefaultGroups);

    /// <summary>Creates a post-interceptor descriptor.</summary>
    /// <param name="messageType">The intercepted message type (generic definitions for generic messages).</param>
    /// <param name="resultType">The result type the interceptor is declared for; <see cref="object"/> for result-agnostic contracts.</param>
    /// <param name="handlerType">The concrete interceptor type to resolve and invoke.</param>
    /// <param name="weight">The execution-order weight (higher runs earlier); 0 when undeclared.</param>
    /// <param name="groups">The interceptor's group names, or <c>null</c> for the default group.</param>
    public static IPostInterceptorDescriptor PostInterceptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight = 0,
        IReadOnlyCollection<string>? groups = null)
        => new GeneratedPostInterceptorDescriptor(messageType, resultType, handlerType, weight, groups ?? DefaultGroups);

    /// <summary>Creates an exception-interceptor descriptor.</summary>
    /// <param name="messageType">The intercepted message type (generic definitions for generic messages).</param>
    /// <param name="resultType">The result type the interceptor is declared for; <see cref="object"/> for result-agnostic contracts.</param>
    /// <param name="handlerType">The concrete interceptor type to resolve and invoke.</param>
    /// <param name="weight">The execution-order weight (higher runs earlier); 0 when undeclared.</param>
    /// <param name="groups">The interceptor's group names, or <c>null</c> for the default group.</param>
    public static IExceptionInterceptorDescriptor ExceptionInterceptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight = 0,
        IReadOnlyCollection<string>? groups = null)
        => new GeneratedExceptionInterceptorDescriptor(messageType, resultType, handlerType, weight, groups ?? DefaultGroups);

    /// <summary>Creates a final-interceptor descriptor.</summary>
    /// <param name="messageType">The intercepted message type (generic definitions for generic messages).</param>
    /// <param name="resultType">The result type the interceptor is declared for; <see cref="object"/> for result-agnostic contracts.</param>
    /// <param name="handlerType">The concrete interceptor type to resolve and invoke.</param>
    /// <param name="weight">The execution-order weight (higher runs earlier); 0 when undeclared.</param>
    /// <param name="groups">The interceptor's group names, or <c>null</c> for the default group.</param>
    public static IFinalInterceptorDescriptor FinalInterceptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight = 0,
        IReadOnlyCollection<string>? groups = null)
        => new GeneratedFinalInterceptorDescriptor(messageType, resultType, handlerType, weight, groups ?? DefaultGroups);

    private abstract class GeneratedDescriptor(
        Type messageType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight,
        IReadOnlyCollection<string> groups)
    {
        public Type MessageType { get; } = messageType;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        public Type HandlerType { get; } = handlerType;

        public uint Weight { get; } = weight;

        public IReadOnlyCollection<string> Groups { get; } = groups;
    }

    private sealed class GeneratedMainHandlerDescriptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight,
        IReadOnlyCollection<string> groups)
        : GeneratedDescriptor(messageType, handlerType, weight, groups), IMainHandlerDescriptor
    {
        public Type ResultType { get; } = resultType;
    }

    private sealed class GeneratedPreInterceptorDescriptor(
        Type messageType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight,
        IReadOnlyCollection<string> groups)
        : GeneratedDescriptor(messageType, handlerType, weight, groups), IPreInterceptorDescriptor;

    private sealed class GeneratedPostInterceptorDescriptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight,
        IReadOnlyCollection<string> groups)
        : GeneratedDescriptor(messageType, handlerType, weight, groups), IPostInterceptorDescriptor
    {
        public Type ResultType { get; } = resultType;
    }

    private sealed class GeneratedExceptionInterceptorDescriptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight,
        IReadOnlyCollection<string> groups)
        : GeneratedDescriptor(messageType, handlerType, weight, groups), IExceptionInterceptorDescriptor
    {
        public Type ResultType { get; } = resultType;
    }

    private sealed class GeneratedFinalInterceptorDescriptor(
        Type messageType,
        Type resultType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
        uint weight,
        IReadOnlyCollection<string> groups)
        : GeneratedDescriptor(messageType, handlerType, weight, groups), IFinalInterceptorDescriptor
    {
        public Type ResultType { get; } = resultType;
    }
}
