using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    ///     Computes the staged pipeline plans: a dispatchable command (void) or
    ///     command/query (single closed result) qualifies when its sole discovered handler
    ///     is the matching async contract AND at least one discovered interceptor
    ///     participates in its pipeline AND every part of that pipeline could be modeled
    ///     exactly — the composition (membership and order) replicates the runtime
    ///     shape-builder, and every call's pattern-match arm is decidable at compile time.
    ///     Anything unmodelable disqualifies the message rather than risking divergence;
    ///     the runtime gate then simply never sees a staged plan for it. The plan stays
    ///     advisory regardless: the hosting executor validates it against the container's
    ///     selected frozen composition.
    /// </summary>
    /// <summary>
    ///     The group sets each dispatch site names, indexed by the static message key the
    ///     site dispatches through. A message's own key and every key it is assignable to
    ///     both count: a site typed as a base (or a marker) delivers the subtype too, so its
    ///     filter is one the subtype's pipeline can be asked for.
    /// </summary>
    /// <remarks>
    ///     Only readable filters land here. A site whose set the scan could not fold names
    ///     no key — its message keeps the runtime group lane, which filters the live
    ///     composition per dispatch and is always correct, merely not straight-line.
    /// </remarks>
    /// <summary>
    ///     The message keys some dispatch site names under a filter this compilation cannot
    ///     read. Those messages get the filtering plan — the body that answers any set.
    /// </summary>
    private static HashSet<string> CollectUnprovableGroupKeys(
        ImmutableArray<DispatchSiteModel> sourceSites,
        ImmutableArray<DispatchSiteModel> referencedSites)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        Add(sourceSites);
        Add(referencedSites);

        return keys;

        void Add(ImmutableArray<DispatchSiteModel> sites)
        {
            foreach (var site in sites)
            {
                if (site.HasUnprovableGroups)
                {
                    keys.Add(TypeExpressions.DefinitionKey(site.MessageTypeExpression));
                }
            }
        }
    }

    /// <summary>
    ///     Whether any unreadable filter can reach this message — through its own type or
    ///     through a base a site was typed as, the same reach a keyed set has.
    /// </summary>
    private static bool HasUnprovableGroupSite(RegistrableTypeModel type, HashSet<string> unprovableKeys)
    {
        if (unprovableKeys.Count == 0)
        {
            return false;
        }

        if (unprovableKeys.Contains(TypeExpressions.DefinitionKey(type.TypeofExpression)))
        {
            return true;
        }

        foreach (var assignableKey in type.AssignableKeys)
        {
            if (unprovableKeys.Contains(TypeExpressions.DefinitionKey(assignableKey)))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, List<ImmutableArray<string>>> CollectGroupSets(
        ImmutableArray<DispatchSiteModel> sourceSites,
        ImmutableArray<DispatchSiteModel> referencedSites)
    {
        var byKey = new Dictionary<string, List<ImmutableArray<string>>>(StringComparer.Ordinal);

        Add(sourceSites);
        Add(referencedSites);

        return byKey;

        void Add(ImmutableArray<DispatchSiteModel> sites)
        {
            foreach (var site in sites)
            {
                // The default set needs no site to prove it: every message gets that plan
                // attempt anyway, so recording it here would only duplicate work.
                if (site.HasUnprovableGroups || site.Groups.IsEmpty)
                {
                    continue;
                }

                var key = TypeExpressions.DefinitionKey(site.MessageTypeExpression);

                if (!byKey.TryGetValue(key, out var sets))
                {
                    byKey.Add(key, sets = []);
                }

                var duplicate = false;

                foreach (var existing in sets)
                {
                    if (GroupSetsEqual(existing, site.Groups))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    sets.Add(site.Groups);
                }
            }
        }
    }

    /// <summary>
    ///     Every group set a dispatch could ask this message's pipeline for: the sets sites
    ///     named for the message itself, plus the ones named for a type it is assignable to
    ///     — a publish typed as a base reaches the subtype, and reaches it under that
    ///     filter.
    /// </summary>
    private static void CollectTargetSets(
        RegistrableTypeModel type,
        Dictionary<string, List<ImmutableArray<string>>> groupSetsByKey,
        List<ImmutableArray<string>> into)
    {
        if (groupSetsByKey.Count == 0)
        {
            return;
        }

        AddFrom(TypeExpressions.DefinitionKey(type.TypeofExpression));

        foreach (var assignableKey in type.AssignableKeys)
        {
            AddFrom(TypeExpressions.DefinitionKey(assignableKey));
        }

        void AddFrom(string key)
        {
            if (!groupSetsByKey.TryGetValue(key, out var sets))
            {
                return;
            }

            foreach (var candidate in sets)
            {
                var duplicate = false;

                foreach (var existing in into)
                {
                    if (GroupSetsEqual(existing, candidate))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    into.Add(candidate);
                }
            }
        }
    }

    private static bool GroupSetsEqual(ImmutableArray<string> left, ImmutableArray<string> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
