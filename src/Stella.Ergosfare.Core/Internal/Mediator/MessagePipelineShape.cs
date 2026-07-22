using Stella.Ergosfare.Contracts.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The provider-independent part of a message's resolved pipeline: descriptors per stage,
/// group-filtered and ordered by weight and handler type name. Shapes depend only on the
/// registry contents and the group set, so they are cached process-wide and shared across
/// scopes — each scope then only materializes cheap lazy wrappers over these arrays.
/// </summary>
internal sealed class MessagePipelineShape
{
    public required IMainHandlerDescriptor[] Handlers { get; init; }
    public required IMainHandlerDescriptor[] IndirectHandlers { get; init; }
    public required IPreInterceptorDescriptor[] PreInterceptors { get; init; }
    public required IPreInterceptorDescriptor[] IndirectPreInterceptors { get; init; }
    public required IPostInterceptorDescriptor[] PostInterceptors { get; init; }
    public required IPostInterceptorDescriptor[] IndirectPostInterceptors { get; init; }
    public required IExceptionInterceptorDescriptor[] ExceptionInterceptors { get; init; }
    public required IExceptionInterceptorDescriptor[] IndirectExceptionInterceptors { get; init; }
    public required IFinalInterceptorDescriptor[] FinalInterceptors { get; init; }
    public required IFinalInterceptorDescriptor[] IndirectFinalInterceptors { get; init; }

    public static MessagePipelineShape Create(IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var groupNames = groups.ToList();
        List<string> effectiveGroups = groupNames.Count == 0 ? [GroupAttribute.DefaultGroupName] : groupNames;

        return new MessagePipelineShape
        {
            Handlers = Prepare(descriptor.Handlers, effectiveGroups),
            IndirectHandlers = Prepare(descriptor.IndirectHandlers, effectiveGroups),
            PreInterceptors = Prepare(descriptor.PreInterceptors, effectiveGroups),
            IndirectPreInterceptors = Prepare(descriptor.IndirectPreInterceptors, effectiveGroups),
            PostInterceptors = Prepare(descriptor.PostInterceptors, effectiveGroups),
            IndirectPostInterceptors = Prepare(descriptor.IndirectPostInterceptors, effectiveGroups),
            ExceptionInterceptors = Prepare(descriptor.ExceptionInterceptors, effectiveGroups),
            IndirectExceptionInterceptors = Prepare(descriptor.IndirectExceptionInterceptors, effectiveGroups),
            FinalInterceptors = Prepare(descriptor.FinalInterceptors, effectiveGroups),
            IndirectFinalInterceptors = Prepare(descriptor.IndirectFinalInterceptors, effectiveGroups),
        };
    }

    /// <summary>
    /// Orders descriptors by weight (descending) then handler type name, and filters by group.
    /// </summary>
    private static TDescriptor[] Prepare<TDescriptor>(IReadOnlyCollection<TDescriptor> descriptors, List<string> groups)
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
            .ToArray();
    }
}
