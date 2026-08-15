using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Everything the Ergosfare attributes say about a participant: its weight, its groups,
///     its discovery keys, and the two exclusions. Read once per type into the model, so the
///     rest of the generator never touches an <c>AttributeData</c> again.
/// </summary>

internal static class ParticipantAttributes
{
    /// <summary>
    ///     Binds the compilation's default adapter to a result slot: a closed adapter by
    ///     exact slot fit, an open definition by unifying the slot against its declared
    ///     carrier patterns and closing over the bound arguments — the compile-time
    ///     mirror of the runtime <c>DefaultResultAdapter</c>'s closing.
    /// </summary>
    /// <summary>
    ///     Whether the type — or its containing assembly — opts out of discovery via
    ///     <c>[ExcludeFromDiscovery]</c>. Excluded types produce no registration and no
    ///     diagnostics: the exclusion is deliberate, unlike an inaccessible type.
    /// </summary>
    internal static bool IsExcludedFromDiscovery(INamedTypeSymbol symbol)
        => HasExcludeFromDiscovery(symbol.GetAttributes())
           || HasExcludeFromDiscovery(symbol.ContainingAssembly.GetAttributes());

    /// <summary>
    ///     Whether the type declares <c>[ExcludeFromPipeline]</c>. The attribute shapes the
    ///     indirect interceptor stages at runtime; staged plans conservatively skip such
    ///     messages instead of modeling the exclusion.
    /// </summary>
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
    ///     The group names a type's <c>[ExcludeFromPipeline]</c> names, or empty for the
    ///     parameterless (blanket) form and for types without the attribute. Mirrors
    ///     <c>MessageDescriptor</c>, which reads the attribute non-inherited.
    /// </summary>
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
    ///     The normalized type expressions of every base type and implemented interface —
    ///     the compile-time domain of the runtime's <c>IsAssignableTo</c> checks that admit
    ///     indirect (covariantly registered) interceptors into a message's pipeline.
    /// </summary>
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
    ///     The type's effective discovery keys: its own <c>[DiscoveryKey]</c> keys when
    ///     declared, else its assembly's. Empty means default discovery (the implicit
    ///     empty-string key) — mirroring the runtime <c>Discovery</c> helper.
    /// </summary>
    internal static ImmutableArray<string> GetDiscoveryKeys(INamedTypeSymbol symbol)
    {
        var keys = GetDeclaredDiscoveryKeys(symbol.GetAttributes());

        return keys.IsEmpty ? GetDeclaredDiscoveryKeys(symbol.ContainingAssembly.GetAttributes()) : keys;
    }

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
    ///     Determines which module markers (<c>ICommand</c>, <c>IQuery</c>, <c>IEvent</c>)
    ///     the type is assignable to. Handlers and interceptors inherit the marker through
    ///     their contract interfaces, so a single check covers messages, handlers and
    ///     interceptors alike.
    /// </summary>
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

    /// <summary>Reads the <c>[Weight]</c> attribute value, or 0 when undeclared.</summary>
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
    ///     The declared <c>[Group]</c> names, empty when the type declares none; the name
    ///     source behind <see cref="GetGroupsExpression"/>.
    /// </summary>
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
    ///     Builds the emitted C# array expression for the <c>[Group]</c> names, or
    ///     <c>null</c> when the type declares none (the descriptor factory then applies the
    ///     default group, matching the reflection path).
    /// </summary>
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
