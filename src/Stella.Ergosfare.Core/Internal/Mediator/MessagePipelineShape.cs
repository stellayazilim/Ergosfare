using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Contracts.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The provider-independent plan of a message's resolved pipeline. Interceptor stages
/// hold direct and indirect registrations merged into a single array — direct entries
/// first, then indirect, each segment ordered by weight (descending) and handler type
/// name. Main handlers keep the direct/indirect split because mediation strategies treat
/// them differently. Each entry carries the pre-computed concrete handler type, so no
/// type resolution happens after the shape is built. Shapes depend only on the registry
/// contents, the message type and the group set, and are cached process-wide.
/// </summary>
internal sealed class MessagePipelineShape
{
    public required PlannedHandler<IMainHandlerDescriptor>[] Handlers { get; init; }
    public required PlannedHandler<IMainHandlerDescriptor>[] IndirectHandlers { get; init; }
    public required PlannedHandler<IPreInterceptorDescriptor>[] PreInterceptors { get; init; }
    public required PlannedHandler<IPostInterceptorDescriptor>[] PostInterceptors { get; init; }
    public required PlannedHandler<IExceptionInterceptorDescriptor>[] ExceptionInterceptors { get; init; }
    public required PlannedHandler<IFinalInterceptorDescriptor>[] FinalInterceptors { get; init; }

    public static MessagePipelineShape Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var groupNames = groups.ToList();
        List<string> effectiveGroups = groupNames.Count == 0 ? [GroupAttribute.DefaultGroupName] : groupNames;

        return new MessagePipelineShape
        {
            Handlers = Prepare(descriptor.Handlers, messageType, effectiveGroups),
            IndirectHandlers = Prepare(descriptor.IndirectHandlers, messageType, effectiveGroups),
            PreInterceptors = Merge(
                Prepare(descriptor.PreInterceptors, messageType, effectiveGroups),
                Prepare(descriptor.IndirectPreInterceptors, messageType, effectiveGroups)),
            PostInterceptors = Merge(
                Prepare(descriptor.PostInterceptors, messageType, effectiveGroups),
                Prepare(descriptor.IndirectPostInterceptors, messageType, effectiveGroups)),
            ExceptionInterceptors = Merge(
                Prepare(descriptor.ExceptionInterceptors, messageType, effectiveGroups),
                Prepare(descriptor.IndirectExceptionInterceptors, messageType, effectiveGroups)),
            FinalInterceptors = Merge(
                Prepare(descriptor.FinalInterceptors, messageType, effectiveGroups),
                Prepare(descriptor.IndirectFinalInterceptors, messageType, effectiveGroups)),
        };
    }

    /// <summary>
    /// Concatenates the direct and indirect segments of an interceptor stage, preserving
    /// the direct-first execution order the invokers previously implemented as two passes.
    /// </summary>
    private static PlannedHandler<TDescriptor>[] Merge<TDescriptor>(
        PlannedHandler<TDescriptor>[] direct,
        PlannedHandler<TDescriptor>[] indirect)
        where TDescriptor : IHandlerDescriptor
    {
        if (indirect.Length == 0)
        {
            return direct;
        }

        if (direct.Length == 0)
        {
            return indirect;
        }

        var merged = new PlannedHandler<TDescriptor>[direct.Length + indirect.Length];
        direct.CopyTo(merged, 0);
        indirect.CopyTo(merged, direct.Length);
        return merged;
    }

    /// <summary>
    /// Filters descriptors by group, orders them by weight (descending) then handler type
    /// name, and pairs each descriptor with its resolved concrete handler type. Plain loops
    /// and a single sort — no LINQ iterators and no per-descriptor set allocations
    /// (the previous <c>Intersect</c> built a hash set per descriptor).
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

        var matched = new List<TDescriptor>(descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            if (MatchesAnyGroup(descriptor.Groups, groups))
            {
                matched.Add(descriptor);
            }
        }

        if (matched.Count == 0)
        {
            return [];
        }

        // Total order (name breaks weight ties), so sort instability cannot reorder equals.
        matched.Sort(static (x, y) =>
        {
            var byWeight = y.Weight.CompareTo(x.Weight);

            return byWeight != 0
                ? byWeight
                : string.CompareOrdinal(x.HandlerType.FullName, y.HandlerType.FullName);
        });

        var planned = new PlannedHandler<TDescriptor>[matched.Count];

        for (var i = 0; i < matched.Count; i++)
        {
            planned[i] = new PlannedHandler<TDescriptor>
            {
                Descriptor = matched[i],
                HandlerType = CloseHandlerType(matched[i], messageType),
            };
        }

        return planned;
    }

    /// <summary>
    /// Whether any of the handler's groups matches any of the requested groups.
    /// </summary>
    private static bool MatchesAnyGroup(IReadOnlyCollection<string> handlerGroups, List<string> groups)
    {
        foreach (var handlerGroup in handlerGroups)
        {
            for (var i = 0; i < groups.Count; i++)
            {
                if (string.Equals(handlerGroup, groups[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the concrete handler type to resolve, closing it over the message's generic
    /// arguments when the descriptor targets a generic message type. Handler types that
    /// are already closed (registered against a constructed generic message) are used as-is.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "Generic handler definitions are closed over message types that are alive in the registry; " +
                        "their generic instantiations are rooted by the handler registrations themselves.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Handler generics are closed over reference message types, which use shared generic code " +
                        "under Native AOT. Source-generated pipeline plans will remove this call entirely.")]
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
