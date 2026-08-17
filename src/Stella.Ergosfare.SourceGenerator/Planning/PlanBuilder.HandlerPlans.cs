
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Builds the plans for messages that produce no result.
    /// </summary>
    /// <param name="types">The discovered types.</param>
    /// <returns>One plan per command that qualifies.</returns>
    /// <remarks>
    /// A command qualifies when its whole discovered pipeline is exactly one main handler,
    /// that handler implements the resultless asynchronous contract, it takes part in default
    /// discovery in the default group, and no discovered interceptor names the message
    /// directly. The test is deliberately cautious and being wrong only costs the speedup:
    /// the executor checks the real pipeline against the composition the container selected
    /// and falls back to the general path on any difference — a covariant handler or
    /// interceptor reached through a base contract, or a keyed registration.
    /// </remarks>
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

            // Only the resultless asynchronous contract; a synchronous handler carries its
            // result type here instead and falls out.
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
    /// Builds the plans for messages that produce a result.
    /// </summary>
    /// <param name="types">The discovered types.</param>
    /// <returns>One plan per command or query that qualifies.</returns>
    /// <remarks>
    /// The counterpart of <see cref="ComputeVoidPlans"/>: a message with exactly one closed,
    /// non-streaming result contract qualifies when its whole pipeline is a single
    /// asynchronous handler producing exactly that result. Just as cautious, and checked
    /// against the container's composition the same way.
    /// </remarks>
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

            // Exactly one closed result, and not a streamed one: a message with several
            // result shapes is dispatched with the result named at the call site, which no
            // single plan can pin down.
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

            // The handler must produce exactly the result the message declares, through the
            // asynchronous contract.
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
    /// Indexes the discovered participants by the message they are registered against.
    /// </summary>
    /// <param name="types">The discovered types.</param>
    /// <param name="handlersByMessage">The main handlers registered against each message.</param>
    /// <param name="interceptedMessages">The messages some interceptor names directly.</param>
    /// <remarks>
    /// Handlers are kept as a list rather than reduced to a count, because how many handlers
    /// serve a pipeline is not a property of the message: groups help decide which of them
    /// run, so the question is only answerable against a particular plan's group set. See
    /// <see cref="TrySelectPlanHandler"/>.
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
    /// Finds the one handler a plan would call, within that plan's own group set.
    /// </summary>
    /// <param name="type">The message the plan serves.</param>
    /// <param name="handlersByMessage">The main handlers registered against each message.</param>
    /// <param name="filter">The plan's group set.</param>
    /// <param name="handler">The selected handler, when this returns <c>true</c>.</param>
    /// <param name="descriptor">That handler's registration, when this returns <c>true</c>.</param>
    /// <returns><c>true</c> when exactly one handler serves this plan.</returns>
    /// <remarks>
    /// A plan is built for one message and one group set, and handlers in different groups
    /// are never both selected — so what decides "one handler serves this pipeline" is how
    /// many are in the plan's own set, not how many the message has in total. The filtering
    /// plan is the exception: it answers any set, so every handler is a candidate and two of
    /// them leave it unable to say which the set at hand selects. That dispatch keeps the
    /// runtime group path.
    /// </remarks>
    private static bool TrySelectPlanHandler(
        List<PlanFinding> findings,
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

            // A second candidate means the plan cannot say which handler serves the
            // dispatch. Where the set is one this compilation can read, that is not a shape
            // to plan around — a send delivers to one handler, so the dispatch throws every
            // time it runs, and the build should say so. The default set is left to ERGO010,
            // which judges ungrouped handlers at the same level.
            if (selected)
            {
                if (!filter.Filtering && !filter.Target.IsEmpty)
                {
                    findings.Add(new PlanFinding(
                        PlanFindingKind.ContestedInGroupSet,
                        type.DisplayName,
                        filter.Target,
                        handler.DisplayName,
                        candidate.Model.DisplayName,
                        type.InfoLocation));
                }

                return false;
            }

            (handler, descriptor) = candidate;
            selected = true;
        }

        return selected;
    }

    /// <summary>
    /// Finds the one handler a single-handler plan would call, and checks it can be planned
    /// for at all.
    /// </summary>
    /// <param name="type">The message the plan would serve.</param>
    /// <param name="handlersByMessage">The main handlers registered against each message.</param>
    /// <param name="interceptedMessages">The messages some interceptor names directly.</param>
    /// <param name="handler">The selected handler, when this returns <c>true</c>.</param>
    /// <param name="descriptor">That handler's registration, when this returns <c>true</c>.</param>
    /// <returns><c>true</c> when the message qualifies for a single-handler plan.</returns>
    /// <remarks>
    /// The message must have exactly one discovered handler and no interceptor naming it,
    /// and that handler must be nameable by generated code and discovered by default —
    /// unkeyed and ungrouped. Anything else might not be registered at all, or not in the
    /// default-group pipeline this plan serves.
    /// </remarks>
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

        // A handler registered against one of the message's base types is not considered
        // here: the message goes to its sole direct handler, which is the one this plan
        // names. The runtime resolves the direct level first too, so plan and pipeline agree.
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
