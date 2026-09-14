using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Extensions;

/// <summary>Compares registered participant stages with the descriptor embedded in a plan.</summary>
internal static class PlanCompositionExtensions
{
    internal static bool MatchesPlan(this PipelineDescriptor descriptor, StagedPlanKey key,
        IReadOnlyList<string> covered, DispatchPlanCatalog catalog, bool local)
    {
        return StageMatches(descriptor.HandlerRows, [], key.HandlerTypeArray, covered, catalog, local)
            && StageMatches(descriptor.IndirectHandlerRows, [], key.IndirectHandlerTypeArray, covered, catalog, local)
            && StageMatches(descriptor.PreInterceptorRows, descriptor.IndirectPreInterceptorRows,
                key.PreInterceptorTypeArray, covered, catalog, local)
            && StageMatches(descriptor.PostInterceptorRows, descriptor.IndirectPostInterceptorRows,
                key.PostInterceptorTypeArray, covered, catalog, local)
            && StageMatches(descriptor.ExceptionInterceptorRows, descriptor.IndirectExceptionInterceptorRows,
                key.ExceptionInterceptorTypeArray, covered, catalog, local)
            && StageMatches(descriptor.FinalInterceptorRows, descriptor.IndirectFinalInterceptorRows,
                key.FinalInterceptorTypeArray, covered, catalog, local);
    }

    internal static bool IsEmptySelection(Type messageType, IReadOnlyList<string> requested, DispatchPlanCatalog catalog)
    {
        var descriptor = catalog.ReadDescriptor(messageType, out var local);
        if (descriptor is null) return true;
        var groups = requested.Count == 0 ? DispatchGroupExtensions.Default : requested;
        return Count(descriptor.HandlerRows, groups, catalog, local) == 0
            && Count(descriptor.IndirectHandlerRows, groups, catalog, local) == 0;
    }

    internal static int Count(FrozenParticipant[] rows, IReadOnlyList<string> groups,
        DispatchPlanCatalog catalog, bool local)
    {
        var count = 0;
        foreach (var row in rows)
            if ((local || catalog.IncludesParticipant(row.HandlerType)) && row.MatchesAnyGroup(groups)) count++;
        return count;
    }

    internal static bool StageMatches(FrozenParticipant[] direct, FrozenParticipant[] indirect, Type[] expected,
        IReadOnlyList<string> groups, DispatchPlanCatalog catalog, bool local)
    {
        var index = 0;
        return SegmentMatches(direct, expected, groups, catalog, local, ref index)
            && SegmentMatches(indirect, expected, groups, catalog, local, ref index)
            && index == expected.Length;
    }

    private static bool SegmentMatches(FrozenParticipant[] rows, Type[] expected,
        IReadOnlyList<string> groups, DispatchPlanCatalog catalog, bool local, ref int index)
    {
        foreach (var row in rows)
        {
            if ((!local && !catalog.IncludesParticipant(row.HandlerType)) || !row.MatchesAnyGroup(groups)) continue;
            if (index == expected.Length || expected[index++] != row.HandlerType) return false;
        }
        return true;
    }
}
