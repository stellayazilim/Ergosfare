using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Reads what the Ergosfare attributes say about a type: its weight, its groups, its
/// discovery keys, and the two exclusions.
/// </summary>
/// <remarks>
/// Read once per type into its model, so nothing downstream touches an
/// <see cref="AttributeData"/> again.
/// </remarks>
internal static class ParticipantAttributes
{
    /// <summary>
    /// Reports whether a type opts out of discovery.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns>
    /// <c>true</c> when the type or its assembly declares <c>[ExcludeFromDiscovery]</c>.
    /// </returns>
    /// <remarks>
    /// An excluded type produces no registration and no diagnostic: unlike an inaccessible
    /// one, its absence is what the author asked for.
    /// </remarks>
    internal static bool IsExcludedFromDiscovery(INamedTypeSymbol symbol)
        => HasExcludeFromDiscovery(symbol.GetAttributes())
           || HasExcludeFromDiscovery(symbol.ContainingAssembly.GetAttributes());

    /// <summary>
    /// Reports whether a type declares <c>[ExcludeFromPipeline]</c>.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns><c>true</c> when the attribute is declared on the type itself.</returns>
    /// <remarks>
    /// The attribute narrows which covariantly matched interceptors reach the message. A
    /// message carrying it gets no staged plan and keeps the general path.
    /// </remarks>
    internal static bool HasPipelineExclusionAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "ExcludeFromPipelineAttribute" } attributeClass
                && SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the groups a type's <c>[ExcludeFromPipeline]</c> names.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>
    /// The named groups; empty both for the parameterless form, which excludes every group,
    /// and for a type without the attribute.
    /// </returns>
    /// <remarks>
    /// Only the type's own declaration is read, never a base type's — the same reach
    /// <c>MessageDescriptor</c> gives the attribute.
    /// </remarks>
    internal static ImmutableArray<string> GetPipelineExclusionGroups(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "ExcludeFromPipelineAttribute" } attributeClass
                || !SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var groups = ImmutableArray.CreateBuilder<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is string name)
                {
                    groups.Add(name);
                }
            }

            return groups.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Collects every type a message is assignable to.
    /// </summary>
    /// <param name="symbol">The message to read.</param>
    /// <returns>
    /// The normalized expressions of its base types and implemented interfaces.
    /// </returns>
    /// <remarks>
    /// This is the compile-time domain of the runtime's assignability check — the one that
    /// admits covariantly registered interceptors into a message's pipeline.
    /// </remarks>
    internal static ImmutableArray<string> GetAssignableKeys(INamedTypeSymbol symbol)
    {
        var keys = ImmutableArray.CreateBuilder<string>();

        for (var baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            keys.Add(SymbolNaming.NormalizedTypeExpression(baseType));
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            keys.Add(SymbolNaming.NormalizedTypeExpression(iface));
        }

        return keys.ToImmutable();
    }

    /// <summary>
    /// Reports whether a list of attributes contains <c>[ExcludeFromDiscovery]</c>.
    /// </summary>
    /// <param name="attributes">The attributes to search.</param>
    /// <returns><c>true</c> when the attribute is among them.</returns>
    internal static bool HasExcludeFromDiscovery(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is { Name: "ExcludeFromDiscoveryAttribute" } attributeClass
                && SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the discovery keys a type is registered under.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>
    /// Its own <c>[DiscoveryKey]</c> keys when it declares any, otherwise its assembly's;
    /// empty means default discovery.
    /// </returns>
    /// <remarks>
    /// The same precedence the runtime <c>Discovery</c> helper applies: a type's own keys
    /// replace its assembly's rather than adding to them.
    /// </remarks>
    internal static ImmutableArray<string> GetDiscoveryKeys(INamedTypeSymbol symbol)
    {
        var keys = GetDeclaredDiscoveryKeys(symbol.GetAttributes());

        return keys.IsEmpty ? GetDeclaredDiscoveryKeys(symbol.ContainingAssembly.GetAttributes()) : keys;
    }

    /// <summary>
    /// Reads the keys a <c>[DiscoveryKey]</c> among these attributes names.
    /// </summary>
    /// <param name="attributes">The attributes to search.</param>
    /// <returns>The declared keys, or empty when none are declared.</returns>
    internal static ImmutableArray<string> GetDeclaredDiscoveryKeys(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { Name: "DiscoveryKeyAttribute" } attributeClass
                || !SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is string key)
                {
                    builder.Add(key);
                }
            }

            return builder.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Determines which module markers a type reaches.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <param name="isCommand">Set when it reaches <c>ICommand</c>.</param>
    /// <param name="isQuery">Set when it reaches <c>IQuery</c>.</param>
    /// <param name="isEvent">Set when it reaches <c>IEvent</c>.</param>
    /// <remarks>
    /// One test covers messages, handlers and interceptors alike: a message declares a marker
    /// itself, and a participant inherits one through the contract it implements. A type can
    /// reach several markers.
    /// </remarks>
    internal static void GetMarkers(INamedTypeSymbol symbol, out bool isCommand, out bool isQuery, out bool isEvent)
    {
        isCommand = false;
        isQuery = false;
        isEvent = false;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity != 0)
            {
                continue;
            }

            switch (iface.Name)
            {
                case ContractNames.CommandMarker when SymbolNaming.IsInNamespace(iface, ContractNames.CommandMarkerNamespace):
                    isCommand = true;
                    break;
                case ContractNames.QueryMarker when SymbolNaming.IsInNamespace(iface, ContractNames.QueryMarkerNamespace):
                    isQuery = true;
                    break;
                case ContractNames.EventMarker when SymbolNaming.IsInNamespace(iface, ContractNames.EventMarkerNamespace):
                    isEvent = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Reads a type's <c>[Weight]</c>.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>The declared weight, or <c>0</c> when it declares none.</returns>
    internal static uint GetWeight(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "WeightAttribute" } attributeClass
                && SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace)
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is uint weight)
            {
                return weight;
            }
        }

        return 0;
    }

    /// <summary>
    /// Reads the groups a type's <c>[Group]</c> names.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>The declared names, or empty when it declares none.</returns>
    /// <remarks>
    /// The names behind <see cref="GetGroupsExpression"/>, kept readable for the decisions
    /// planning makes about group membership.
    /// </remarks>
    internal static ImmutableArray<string> GetGroupNames(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "GroupAttribute" } attributeClass
                || !SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var names = ImmutableArray.CreateBuilder<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is string name)
                {
                    names.Add(name);
                }
            }

            return names.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Writes a type's <c>[Group]</c> names as the array expression the registration emits.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <returns>
    /// The array expression, or <c>null</c> when the type declares no groups — the descriptor
    /// then applies the default group, as it does on the reflection path.
    /// </returns>
    internal static string? GetGroupsExpression(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "GroupAttribute" } attributeClass
                || !SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return null;
            }

            var sb = new StringBuilder("new string[] { ");

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Value is not string name)
                {
                    continue;
                }

                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(SymbolDisplay.FormatLiteral(name, quote: true));
            }

            sb.Append(" }");
            return sb.ToString();
        }

        return null;
    }
}
