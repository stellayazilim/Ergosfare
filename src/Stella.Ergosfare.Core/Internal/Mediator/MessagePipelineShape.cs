using Stella.Ergosfare.Contracts.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The provider-independent plan of a message's resolved pipeline: per stage, the
/// group-filtered descriptors ordered by weight and handler type name, each paired with
/// the pre-computed concrete handler type to resolve. Shapes depend only on the registry
/// contents, the message type and the group set, so they are cached process-wide and
/// shared across scopes — each scope then only materializes cheap lazy wrappers over
/// these arrays, without re-doing any type resolution.
/// </summary>
internal sealed class MessagePipelineShape
{
    public required PlannedHandler<IMainHandlerDescriptor>[] Handlers { get; init; }
    public required PlannedHandler<IMainHandlerDescriptor>[] IndirectHandlers { get; init; }
    public required PlannedHandler<IPreInterceptorDescriptor>[] PreInterceptors { get; init; }
    public required PlannedHandler<IPreInterceptorDescriptor>[] IndirectPreInterceptors { get; init; }
    public required PlannedHandler<IPostInterceptorDescriptor>[] PostInterceptors { get; init; }
    public required PlannedHandler<IPostInterceptorDescriptor>[] IndirectPostInterceptors { get; init; }
    public required PlannedHandler<IExceptionInterceptorDescriptor>[] ExceptionInterceptors { get; init; }
    public required PlannedHandler<IExceptionInterceptorDescriptor>[] IndirectExceptionInterceptors { get; init; }
    public required PlannedHandler<IFinalInterceptorDescriptor>[] FinalInterceptors { get; init; }
    public required PlannedHandler<IFinalInterceptorDescriptor>[] IndirectFinalInterceptors { get; init; }

    public static MessagePipelineShape Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var groupNames = groups.ToList();
        List<string> effectiveGroups = groupNames.Count == 0 ? [GroupAttribute.DefaultGroupName] : groupNames;

        return new MessagePipelineShape
        {
            Handlers = Prepare(descriptor.Handlers, messageType, effectiveGroups),
            IndirectHandlers = Prepare(descriptor.IndirectHandlers, messageType, effectiveGroups),
            PreInterceptors = Prepare(descriptor.PreInterceptors, messageType, effectiveGroups),
            IndirectPreInterceptors = Prepare(descriptor.IndirectPreInterceptors, messageType, effectiveGroups),
            PostInterceptors = Prepare(descriptor.PostInterceptors, messageType, effectiveGroups),
            IndirectPostInterceptors = Prepare(descriptor.IndirectPostInterceptors, messageType, effectiveGroups),
            ExceptionInterceptors = Prepare(descriptor.ExceptionInterceptors, messageType, effectiveGroups),
            IndirectExceptionInterceptors = Prepare(descriptor.IndirectExceptionInterceptors, messageType, effectiveGroups),
            FinalInterceptors = Prepare(descriptor.FinalInterceptors, messageType, effectiveGroups),
            IndirectFinalInterceptors = Prepare(descriptor.IndirectFinalInterceptors, messageType, effectiveGroups),
        };
    }

    /// <summary>
    /// Orders descriptors by weight (descending) then handler type name, filters by group,
    /// and pairs each descriptor with its resolved concrete handler type.
    /// </summary>
    private static PlannedHandler<TDescriptor>[] Prepare<TDescriptor>(
        IReadOnlyCollection<TDescriptor> descriptors,
        Type messageType,
        List<string> groups)
        where TDescriptor : IHandlerDescriptor
    {
        if (descriptors.Count == 0)
        {
            return [];
        }

        return descriptors
            .OrderByDescending(d => d.Weight)
            .ThenBy(d => d.HandlerType.FullName, StringComparer.Ordinal)
            .Where(d => d.Groups.Intersect(groups).Any())
            .Select(d => new PlannedHandler<TDescriptor>
            {
                Descriptor = d,
                HandlerType = CloseHandlerType(d, messageType),
            })
            .ToArray();
    }

    /// <summary>
    /// Returns the concrete handler type to resolve, closing it over the message's generic
    /// arguments when the descriptor targets a generic message type. Handler types that
    /// are already closed (registered against a constructed generic message) are used as-is.
    /// </summary>
    private static Type CloseHandlerType(IHandlerDescriptor descriptor, Type messageType)
    {
        var handlerType = descriptor.HandlerType;

        if (descriptor.MessageType.IsGenericType && handlerType.IsGenericTypeDefinition)
        {
            handlerType = handlerType.MakeGenericType(messageType.GetGenericArguments());
        }

        return handlerType;
    }
}
