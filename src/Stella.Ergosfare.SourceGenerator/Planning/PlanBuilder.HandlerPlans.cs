
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
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

}
