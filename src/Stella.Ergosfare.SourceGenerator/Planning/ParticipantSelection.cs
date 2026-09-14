using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>Selects the application's participants before validation or plan construction.</summary>
internal static class ParticipantSelection
{
    internal static ImmutableArray<RegistrableTypeModel> Select(
        ImmutableArray<RegistrableTypeModel> candidates, ImmutableArray<RegistrationSiteModel> requests)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        foreach (var request in requests)
        {
            if (request.TypeExpression is { } type
                && (candidate.TypeofExpression == type || candidate.MonomorphizedFrom == type))
                selected.Add(candidate.TypeofExpression);
            if (request.DiscoveryPattern is not { } pattern || candidate.IsExcludedFromDiscovery) continue;
            if (request.Module switch { 1 => !candidate.IsCommand, 2 => !candidate.IsQuery, 3 => !candidate.IsEvent, _ => false }) continue;
            if (candidate.DiscoveryKeys.IsEmpty ? Matches("", pattern) : candidate.DiscoveryKeys.Any(key => Matches(key, pattern)))
                selected.Add(candidate.TypeofExpression);
        }

        // A selected main handler brings the message contract it serves. Covariant message
        // shapes are also executable, but never bring additional participants with them.
        var served = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
            if (selected.Contains(candidate.TypeofExpression))
                foreach (var descriptor in candidate.Descriptors)
                    if (descriptor.Kind == DescriptorKind.MainHandler)
                        served.Add(TypeExpressions.DefinitionKey(descriptor.MessageTypeExpression));

        return candidates.Where(candidate => selected.Contains(candidate.TypeofExpression)
            || candidate.IsMessageShape && (served.Contains(TypeExpressions.DefinitionKey(candidate.TypeofExpression))
                || candidate.AssignableKeys.Any(key => served.Contains(TypeExpressions.DefinitionKey(key)))))
            .ToImmutableArray();
    }

    private static bool Matches(string key, string pattern)
        => pattern.EndsWith("*", StringComparison.Ordinal)
            ? key.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal)
            : string.Equals(key, pattern, StringComparison.Ordinal);
}
