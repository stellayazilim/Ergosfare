using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Whether a participant can be built with <c>new</c> instead of resolved, and how.
///     A plan constructs participants directly only where doing so is observably identical
///     to container activation, so every condition here is a condition of that equality:
///     a public parameterless constructor, or one whose parameters the generated code can
///     itself resolve from the dispatching provider.
/// </summary>

internal static class ConstructionAnalyzer
{
    /// <summary>
    ///     Whether generated code can construct the type with <c>new()</c> and doing so
    ///     is provably interchangeable with resolving its plain transient registration:
    ///     a concrete, non-generic class whose ONLY instance constructor is public and
    ///     parameterless (the container's greedy constructor selection would pick any
    ///     richer constructor, and it only considers public ones), with no <c>required</c>
    ///     members (a generated <c>new()</c> would fail compilation), implementing
    ///     neither <c>IDisposable</c> nor <c>IAsyncDisposable</c> (the container tracks
    ///     transient disposables; direct construction would not).
    /// </summary>
    internal static bool IsDirectlyConstructible(INamedTypeSymbol symbol)
    {
        if (!HasDirectConstructionShape(symbol))
        {
            return false;
        }

        // Exactly one instance constructor, public and parameterless: with any richer
        // constructor present (records' synthesized copy constructor included), the
        // container's selection and `new()` can diverge — dropping dependencies the
        // container would have injected.
        return symbol.InstanceConstructors.Length == 1
               && symbol.InstanceConstructors[0] is { Parameters.IsEmpty: true, DeclaredAccessibility: Accessibility.Public };
    }

    /// <summary>
    ///     Shared base qualification of both construction factories: a concrete,
    ///     non-generic class implementing neither <c>IDisposable</c> nor
    ///     <c>IAsyncDisposable</c> (the container tracks transient disposables in the
    ///     resolving scope; direct construction would not), with no <c>required</c>
    ///     members anywhere in the hierarchy (an emitted <c>new</c> fails compilation
    ///     with CS9035, while the container activation the factory replaces ignores them).
    /// </summary>
    internal static bool HasDirectConstructionShape(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.IsGenericType)
        {
            return false;
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface is { Name: "IDisposable" or "IAsyncDisposable", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } })
            {
                return false;
            }
        }

        for (var type = symbol; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Builds the provider-taking construction factory
    ///     (<c>static provider =&gt; new THandler(...)</c>) for a handler whose
    ///     construction is provably identical to container activation, or <c>null</c>
    ///     when the type does not qualify. The gate mirrors what
    ///     <c>Microsoft.Extensions.DependencyInjection</c> would do with the type's plain
    ///     transient registration: the container considers only public constructors, so a
    ///     type with exactly one public constructor leaves it no choice; every parameter
    ///     must be a plain service resolution (<c>GetRequiredService</c>) or a
    ///     <c>[FromKeyedServices]</c> one (<c>GetRequiredKeyedService</c>) from the very
    ///     provider container activation would resolve from. Anything that makes the
    ///     container's behavior content-dependent disqualifies: optional/default-valued
    ///     parameters (the container falls back to the default only when the service is
    ///     unregistered), multiple public constructors (greedy selection), <c>ref</c>-ish
    ///     or <c>params</c> parameters, <c>[ServiceKey]</c> injection, non-nameable
    ///     parameter types, and keys the emission cannot reproduce exactly.
    /// </summary>
    internal static string? GetProviderConstructionExpression(
        INamedTypeSymbol symbol,
        string handlerTypeExpression,
        IAssemblySymbol? currentAssembly,
        out bool usesKeyedServices)
    {
        var construction = TryBuildConstructionExpression(
            symbol, handlerTypeExpression, currentAssembly, "provider", allowParameterless: false, out usesKeyedServices);

        return construction is null ? null : "static provider => " + construction;
    }

    /// <summary>
    ///     Builds the bare <c>new T(...)</c> expression for a participant whose
    ///     construction is provably identical to container activation (see
    ///     <see cref="GetProviderConstructionExpression"/> for the gate), resolving
    ///     constructor dependencies from the given provider identifier. The staged plans'
    ///     direct-construction emission consumes it with <c>serviceProvider</c>; the
    ///     provider factories wrap it in a lambda. Parameterless constructions are only
    ///     produced when asked for — the plan factories keep those on the cheaper
    ///     <c>Func&lt;THandler&gt;</c> shape.
    /// </summary>
    internal static string? TryBuildConstructionExpression(
        INamedTypeSymbol symbol,
        string typeExpression,
        IAssemblySymbol? currentAssembly,
        string providerIdentifier,
        bool allowParameterless,
        out bool usesKeyedServices)
    {
        usesKeyedServices = false;

        if (!HasDirectConstructionShape(symbol))
        {
            return null;
        }

        IMethodSymbol? publicConstructor = null;

        foreach (var constructor in symbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                // Invisible to the container's constructor selection; irrelevant here too.
                continue;
            }

            if (publicConstructor is not null)
            {
                return null;
            }

            publicConstructor = constructor;
        }

        if (publicConstructor is null)
        {
            return null;
        }

        if (publicConstructor.Parameters.IsEmpty)
        {
            return allowParameterless ? "new " + typeExpression + "()" : null;
        }

        var arguments = new List<string>(publicConstructor.Parameters.Length);

        foreach (var parameter in publicConstructor.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.IsOptional || parameter.HasExplicitDefaultValue)
            {
                return null;
            }

            string? keyLiteral = null;

            foreach (var attribute in parameter.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeClass
                    || !IsDependencyInjectionNamespace(attributeClass.ContainingNamespace))
                {
                    continue;
                }

                switch (attributeClass.Name)
                {
                    case "FromKeyedServicesAttribute":
                        keyLiteral = GetServiceKeyLiteral(attribute, currentAssembly);

                        if (keyLiteral is null)
                        {
                            return null;
                        }

                        break;
                    case "ServiceKeyAttribute":
                        return null;
                }
            }

            if (parameter.Type is not INamedTypeSymbol parameterType
                || parameterType.IsRefLikeType
                || parameterType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                || !IsNameableClosedType(parameterType, currentAssembly))
            {
                return null;
            }

            var parameterTypeExpression = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            arguments.Add(keyLiteral is null
                ? "global::" + ContractMetadataNames.ServiceProviderExtensions + ".GetRequiredService<" + parameterTypeExpression + ">(" + providerIdentifier + ")"
                : "global::" + ContractMetadataNames.KeyedServiceExtensions + ".GetRequiredKeyedService<" + parameterTypeExpression + ">(" + providerIdentifier + ", " + keyLiteral + ")");

            usesKeyedServices |= keyLiteral is not null;
        }

        return "new " + typeExpression + "(" + string.Join(", ", arguments) + ")";
    }

    /// <summary>
    ///     Whether generated code in the current compilation can name the closed type in
    ///     a generic argument position: spellable names and public accessibility along the
    ///     whole containing chain (internal accepted only for the current compilation's
    ///     own types — referenced-assembly IVT grants are deliberately not modeled here),
    ///     recursively for every generic type argument.
    /// </summary>
    internal static bool IsNameableClosedType(INamedTypeSymbol type, IAssemblySymbol? currentAssembly)
    {
        if (type.IsUnboundGenericType || !SymbolNaming.HasSpellableName(type))
        {
            return false;
        }

        for (var current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    break;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (currentAssembly is null
                        || !SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, currentAssembly))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        foreach (var argument in type.TypeArguments)
        {
            if (argument is not INamedTypeSymbol named || !IsNameableClosedType(named, currentAssembly))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsDependencyInjectionNamespace(INamespaceSymbol? ns)
        => ns is
        {
            Name: "DependencyInjection",
            ContainingNamespace:
            {
                Name: "Extensions",
                ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true }
            }
        };

    /// <summary>
    ///     The C# literal reproducing a <c>[FromKeyedServices]</c> key exactly — the
    ///     container matches keys by boxed equality, so the emitted constant must carry
    ///     the same runtime type and value as the attribute's. Strings, chars, bools,
    ///     integral primitives, enums and <c>typeof</c> keys are reproducible; anything
    ///     else (null, floating-point, arrays) returns <c>null</c> and keeps the handler
    ///     on the container path.
    /// </summary>
    internal static string? GetServiceKeyLiteral(AttributeData attribute, IAssemblySymbol? currentAssembly)
    {
        if (attribute.ConstructorArguments.Length != 1)
        {
            return null;
        }

        var key = attribute.ConstructorArguments[0];

        switch (key.Kind)
        {
            case TypedConstantKind.Primitive:
                return key.Value switch
                {
                    string s => SymbolDisplay.FormatLiteral(s, quote: true),
                    char c => SymbolDisplay.FormatLiteral(c, quote: true),
                    bool b => b ? "true" : "false",
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    long l => l.ToString(CultureInfo.InvariantCulture) + "L",
                    sbyte v => "(sbyte)" + v.ToString(CultureInfo.InvariantCulture),
                    byte v => "(byte)" + v.ToString(CultureInfo.InvariantCulture),
                    short v => "(short)" + v.ToString(CultureInfo.InvariantCulture),
                    ushort v => "(ushort)" + v.ToString(CultureInfo.InvariantCulture),
                    uint v => v.ToString(CultureInfo.InvariantCulture) + "U",
                    ulong v => v.ToString(CultureInfo.InvariantCulture) + "UL",
                    _ => null,
                };
            case TypedConstantKind.Enum:
                return key.Type is INamedTypeSymbol enumType && IsNameableClosedType(enumType, currentAssembly)
                    ? "(" + enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")("
                      + Convert.ToString(key.Value, CultureInfo.InvariantCulture) + ")"
                    : null;
            case TypedConstantKind.Type:
                return key.Value is INamedTypeSymbol { IsUnboundGenericType: false } keyType
                       && IsNameableClosedType(keyType, currentAssembly)
                    ? "typeof(" + keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")"
                    : null;
            default:
                return null;
        }
    }

    internal static bool HasMultiplePublicInstanceConstructors(INamedTypeSymbol symbol)
    {
        var count = 0;

        foreach (var constructor in symbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility == Accessibility.Public && ++count > 1)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasFromServicesOnConstructor(INamedTypeSymbol symbol)
    {
        foreach (var constructor in symbol.InstanceConstructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                foreach (var attribute in parameter.GetAttributes())
                {
                    if (attribute.AttributeClass is { Name: "FromServicesAttribute" })
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
