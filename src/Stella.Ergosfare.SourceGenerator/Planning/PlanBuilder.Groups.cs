using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Collects the messages that some dispatch site asks for under a group set this
    /// compilation cannot read.
    /// </summary>
    /// <param name="sourceSites">The dispatch sites in this compilation.</param>
    /// <param name="referencedSites">The dispatch sites referenced assemblies recorded.</param>
    /// <returns>The message keys such a site names.</returns>
    /// <remarks>
    /// Those messages get the filtering plan — the one body that can answer any set.
    /// </remarks>
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
    /// Reports whether an unreadable group set can reach a message.
    /// </summary>
    /// <param name="type">The message to test.</param>
    /// <param name="unprovableKeys">The messages named by unreadable sets.</param>
    /// <returns><c>true</c> when such a set reaches this message.</returns>
    /// <remarks>
    /// Reached either through the message's own type or through a base type some site was
    /// written against — the same reach a readable set has.
    /// </remarks>
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

    /// <summary>
    /// Collects the group sets dispatch sites name, indexed by the message they dispatch.
    /// </summary>
    /// <param name="sourceSites">The dispatch sites in this compilation.</param>
    /// <param name="referencedSites">The dispatch sites referenced assemblies recorded.</param>
    /// <returns>The distinct sets named for each message key.</returns>
    /// <remarks>
    /// Only readable sets are collected. A site whose set could not be read names no key
    /// here; its message keeps the runtime group path, which filters the live composition on
    /// each dispatch — always correct, just not a straight line.
    /// </remarks>
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
                // The default set needs no site to name it: every message is tried against
                // that plan anyway, so recording it would only duplicate the work.
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
    /// Collects every group set a dispatch could ask one message's pipeline for.
    /// </summary>
    /// <param name="type">The message whose plans are being built.</param>
    /// <param name="groupSetsByKey">The sets named for each message key.</param>
    /// <param name="into">The list to add the distinct sets to.</param>
    /// <remarks>
    /// Both the sets named for the message itself and those named for a type it is
    /// assignable to: a publish written against a base type reaches the derived message, and
    /// reaches it under that set.
    /// </remarks>
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

    /// <summary>
    /// Compares two group sets.
    /// </summary>
    /// <param name="left">The first set.</param>
    /// <param name="right">The second set.</param>
    /// <returns><c>true</c> when both name the same groups in the same order.</returns>
    /// <remarks>
    /// Order can be compared directly because every set reaching here is already in its
    /// canonical form.
    /// </remarks>
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
