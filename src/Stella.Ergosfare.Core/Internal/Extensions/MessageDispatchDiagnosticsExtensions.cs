using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Extensions;

/// <summary>Explains missing or incompatible plans on the dispatch failure path.</summary>
internal static class MessageDispatchDiagnosticsExtensions
{
    internal static UnplannedDispatchException Missing(Type messageType, UnplannedDispatchReason reason)
        => new(messageType, reason,
            $"No generated plan serves '{messageType}' with this dispatch shape. Rebuild the application with the source generator.");

    // Cold diagnostics only. An available plan executes without revalidating its
    // descriptor or consulting an adapter service.
    internal static void ValidatePlan(this MessageDispatchEngine engine, Type messageType, ICompiledPlan? plan, IReadOnlyList<string> requested,
        bool broadcast)
    {
        var catalog = engine.Catalog;
        if (plan is not null)
        {
            var registered = catalog.ReadDescriptor(messageType, out var isLocal);
            var covered = plan.FilterGroups ?? (requested.Count == 0 ? DispatchGroupExtensions.Default : requested);
            if (registered is null || !registered.MatchesPlan(plan.Composition, covered, catalog, isLocal))
            {
                if (broadcast && PlanCompositionExtensions.IsEmptySelection(messageType, requested, catalog))
                    throw new NoHandlerFoundException(messageType);
                throw new UnplannedDispatchException(messageType, UnplannedDispatchReason.CompositionDiverged,
                    $"The registrations for '{messageType}' do not match its generated plan descriptor: {DescribeMismatch(messageType, plan, requested, catalog)}.");
            }
            throw Missing(messageType, UnplannedDispatchReason.NoCompiledPlan);
        }

        var descriptor = catalog.ReadDescriptor(messageType, out var local);
        if (descriptor is null)
        {
            throw new NoHandlerFoundException(messageType);
        }

        var effective = requested.Count == 0 ? DispatchGroupExtensions.Default : requested;
        var directCount = PlanCompositionExtensions.Count(descriptor.HandlerRows, effective, catalog, local);
        var indirectCount = PlanCompositionExtensions.Count(descriptor.IndirectHandlerRows, effective, catalog, local);
        if (directCount + indirectCount == 0) throw new NoHandlerFoundException(messageType);
        if (!broadcast)
        {
            var count = directCount == 0 ? indirectCount : directCount;
            if (count == 0) throw new NoHandlerFoundException(messageType);
            if (count > 1) throw new MultipleHandlerFoundException(messageType, count);
        }
        throw Missing(messageType, requested.Count == 0
            ? UnplannedDispatchReason.NoCompiledPlan : UnplannedDispatchReason.UnplannedGroupSet);

    }

    private static string DescribeMismatch(Type messageType, ICompiledPlan plan, IReadOnlyList<string> requested,
        DispatchPlanCatalog catalog)
    {
        var descriptor = catalog.ReadDescriptor(messageType, out var local);
        if (descriptor is null) return "no registered descriptor";
        var groups = plan.FilterGroups ?? (requested.Count == 0 ? DispatchGroupExtensions.Default : requested);
        var key = plan.Composition;
        if (!PlanCompositionExtensions.StageMatches(descriptor.HandlerRows, [], key.HandlerTypeArray, groups, catalog, local))
            return Describe("handlers", key.HandlerTypeArray);
        if (!PlanCompositionExtensions.StageMatches(descriptor.IndirectHandlerRows, [], key.IndirectHandlerTypeArray, groups, catalog, local))
            return Describe("indirect handlers", key.IndirectHandlerTypeArray);
        if (!PlanCompositionExtensions.StageMatches(descriptor.PreInterceptorRows, descriptor.IndirectPreInterceptorRows, key.PreInterceptorTypeArray, groups, catalog, local))
            return Describe("pre-interceptors", key.PreInterceptorTypeArray);
        if (!PlanCompositionExtensions.StageMatches(descriptor.PostInterceptorRows, descriptor.IndirectPostInterceptorRows, key.PostInterceptorTypeArray, groups, catalog, local))
            return Describe("post-interceptors", key.PostInterceptorTypeArray);
        if (!PlanCompositionExtensions.StageMatches(descriptor.ExceptionInterceptorRows, descriptor.IndirectExceptionInterceptorRows, key.ExceptionInterceptorTypeArray, groups, catalog, local))
            return Describe("exception interceptors", key.ExceptionInterceptorTypeArray);
        return Describe("final interceptors", key.FinalInterceptorTypeArray);

        static string Describe(string stage, Type[] expected)
            => $"{stage}: expected [{string.Join(", ", expected.Select(t => t.Name))}]";
    }
}
