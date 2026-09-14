using Stella.Ergosfare.Core.Abstractions.Planning;

namespace Stella.Ergosfare.Core.Internal.Extensions;

/// <summary>Binds existing generated plans to the container during engine construction.</summary>
internal static class MessageDispatchPreparationExtensions
{
    internal static void PreparePlans(this MessageDispatchEngine engine)
    {
        var catalog = engine.Catalog;
        catalog.Seal();
        foreach (var entry in GeneratedPlanRegistry.PlanEntries)
        {
            var descriptor = catalog.ReadDescriptor(entry.MessageType, out var local);
            var covered = entry.Plan.FilterGroups ?? (entry.Groups.Count == 0 ? DispatchGroupExtensions.Default : entry.Groups);
            catalog.BindPlan(entry.MessageType, entry.ResultType, entry.Kind, entry.Groups, entry.Plan,
                descriptor is not null
                && descriptor.MatchesPlan(entry.Plan.Composition, covered, catalog, local));
        }
    }
}
