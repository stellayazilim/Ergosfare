using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    ///     Computes the compile-time void pipeline plans: a dispatchable command message
    ///     qualifies when the whole discovered pipeline for it is exactly one main-handler
    ///     descriptor, that descriptor is the result-less async contract
    ///     (<c>IAsyncHandler&lt;TMessage&gt;</c>), its handler participates in default
    ///     discovery in the default group, and no discovered interceptor targets the
    ///     message directly. The check is deliberately conservative and only ever costs
    ///     the speedup when wrong: the runtime executor validates the actual pipeline
    ///     against the container's selected frozen composition and falls back to the
    ///     general dispatch shape on any mismatch (covariant handlers or interceptors
    ///     selected through base contracts, or keyed selections).
    /// </summary>
    private static List<VoidPlanModel> ComputeVoidPlans(List<RegistrableTypeModel> types)
    {
        CollectPipelineFacts(types, out var handlersByMessage, out var interceptedMessages);

        var plans = new List<VoidPlanModel>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || !type.IsCommand)
            {
                continue;
            }

            if (!TryGetSolePlannableHandler(type, handlersByMessage, interceptedMessages,
                    out var handler, out var descriptor))
            {
                continue;
            }

            // The sole handler must be the async void contract.
            if (descriptor.ResultTypeExpression != EmittedExpressions.ValueTask)
            {
                continue;
            }

            plans.Add(new VoidPlanModel(
                type.TypeofExpression,
                handler.TypeofExpression,
                handler.IsDirectlyConstructible,
                handler.ProviderConstructionExpression,
                handler.ProviderConstructionUsesKeyedServices));
        }

        return plans;
    }

    /// <summary>
    ///     Result-producing counterpart of <see cref="ComputeVoidPlans"/>: a dispatchable
    ///     command/query with exactly one closed, non-stream result contract qualifies
    ///     when its whole discovered pipeline is a single async handler producing exactly
    ///     that result. Equally conservative and equally advisory — the runtime validates
    ///     it against the container's selected frozen composition.
    /// </summary>
    private static List<ResultPlanModel> ComputeResultPlans(List<RegistrableTypeModel> types)
    {
        CollectPipelineFacts(types, out var handlersByMessage, out var interceptedMessages);

        var plans = new List<ResultPlanModel>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || (!type.IsCommand && !type.IsQuery))
            {
                continue;
            }

            // Exactly one closed result contract, and not a streaming one — a message
            // with several result shapes is dispatched with executor-side result typing
            // the plan cannot pin down.
            if (type.DispatchResults.Length != 1 || type.DispatchResults[0].IsStream)
            {
                continue;
            }

            var dispatchResult = type.DispatchResults[0];

            if (!TryGetSolePlannableHandler(type, handlersByMessage, interceptedMessages,
                    out var handler, out var descriptor))
            {
                continue;
            }

            // The sole handler must be the async contract producing exactly the message's
            // declared result (sync contracts carry the bare result type and fall out).
            if (descriptor.ResultTypeExpression != EmittedExpressions.ValueTask + "<" + dispatchResult.ResultTypeExpression + ">")
            {
                continue;
            }

            plans.Add(new ResultPlanModel(
                type.TypeofExpression,
                dispatchResult.ResultTypeExpression,
                handler.TypeofExpression,
                handler.IsDirectlyConstructible,
                handler.ProviderConstructionExpression,
                handler.ProviderConstructionUsesKeyedServices));
        }

        return plans;
    }

    /// <summary>
    ///     Indexes the discovered descriptors by message type: every main handler registered
    ///     against a message, and the set of messages any interceptor targets directly — the
    ///     shared facts all three plan computations qualify against.
    /// </summary>
    /// <remarks>
    ///     The handlers are kept as a list rather than a count and a last-seen winner because
    ///     "how many handlers serve this pipeline" is not a property of the message: a group
    ///     set is part of what selects them, so the question is only answerable against a
    ///     plan's own set. See <see cref="TrySelectPlanHandler"/>.
    /// </remarks>
    private static void CollectPipelineFacts(
        List<RegistrableTypeModel> types,
        out Dictionary<string, List<(RegistrableTypeModel Model, DescriptorModel Descriptor)>> handlersByMessage,
        out HashSet<string> interceptedMessages)
    {
        handlersByMessage = new Dictionary<string, List<(RegistrableTypeModel, DescriptorModel)>>(StringComparer.Ordinal);
        interceptedMessages = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            foreach (var descriptor in type.Descriptors)
            {
                if (descriptor.Kind == DescriptorKind.MainHandler)
                {
                    if (!handlersByMessage.TryGetValue(descriptor.MessageTypeExpression, out var handlers))
                    {
                        handlers = [];
                        handlersByMessage.Add(descriptor.MessageTypeExpression, handlers);
                    }

                    handlers.Add((type, descriptor));
                }
                else
                {
                    interceptedMessages.Add(descriptor.MessageTypeExpression);
                }
            }
        }
    }

    /// <summary>
    ///     The single-handler gate, taken per plan. A plan is built for one (message, group
    ///     set) pair, and handlers in different groups are never both selected — so the count
    ///     that decides "one handler serves this pipeline" is the count within the plan's own
    ///     set, not the message's registration count.
    /// </summary>
    /// <remarks>
    ///     The filtering plan is the exception: it answers any set, so every handler is a
    ///     candidate for it and two of them leave it unable to say which one the set at hand
    ///     selects. That dispatch keeps the runtime group lane.
    /// </remarks>
    private static bool TrySelectPlanHandler(
        RegistrableTypeModel type,
        Dictionary<string, List<(RegistrableTypeModel Model, DescriptorModel Descriptor)>> handlersByMessage,
        PlanGroupFilter filter,
        out RegistrableTypeModel handler,
        out DescriptorModel descriptor)
    {
        handler = default;
        descriptor = default;

        if (!handlersByMessage.TryGetValue(type.TypeofExpression, out var candidates))
        {
            return false;
        }

        var selected = false;

        foreach (var candidate in candidates)
        {
            if (!filter.Filtering && !PlanGroupFilter.Participates(candidate.Model, filter.Target))
            {
                continue;
            }

            if (selected)
            {
                return false;
            }

            (handler, descriptor) = candidate;
            selected = true;
        }

        return selected;
    }

    /// <summary>
    ///     The shared plan qualification: the message has exactly one discovered main
    ///     handler, no interceptor targets it directly, and that handler is accessible
    ///     and discoverable by default (an unkeyed, ungrouped registration — anything
    ///     else may not be registered, or not in the default-group pipeline the plan
    ///     serves).
    /// </summary>
    private static bool TryGetSolePlannableHandler(
        RegistrableTypeModel type,
        Dictionary<string, List<(RegistrableTypeModel Model, DescriptorModel Descriptor)>> handlersByMessage,
        HashSet<string> interceptedMessages,
        out RegistrableTypeModel handler,
        out DescriptorModel descriptor)
    {
        handler = default;
        descriptor = default;

        if (!handlersByMessage.TryGetValue(type.TypeofExpression, out var candidates) || candidates.Count != 1)
        {
            return false;
        }

        // A handler on one of the message's base contracts is not consulted: the ladder
        // gives the message to its sole direct handler, which is the one baked here. The
        // runtime says the same — MessageDependencies.FastSingleHandler resolves the direct
        // level first — so the plan and the pipeline it stands in for agree.
        if (interceptedMessages.Contains(type.TypeofExpression))
        {
            return false;
        }

        (handler, descriptor) = candidates[0];

        return handler.IsAccessible
               && handler.DiscoveryKeys.IsEmpty
               && handler.GroupsExpression is null;
    }
}
