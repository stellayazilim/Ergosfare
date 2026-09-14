using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Decides whether a participant can be built with <c>new</c> instead of resolved, and how.
/// </summary>
/// <remarks>
/// A plan constructs directly only where doing so is indistinguishable from container
/// activation, so every condition here serves that equality: a public parameterless
/// constructor, or one whose parameters the generated code can resolve from the dispatching
/// provider itself.
/// </remarks>
internal static class ConstructionAnalyzer
{
    /// <summary>
    /// Reports whether a type can be built with a bare <c>new()</c>.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns>
    /// <c>true</c> when doing so is interchangeable with resolving its plain transient
    /// registration.
    /// </returns>
    /// <remarks>
    /// It must be a concrete, non-generic class whose only instance constructor is public and
    /// parameterless, with no <c>required</c> members and implementing neither
    /// <c>IDisposable</c> nor <c>IAsyncDisposable</c>.
    /// </remarks>
    internal static bool IsDirectlyConstructible(INamedTypeSymbol symbol)
    {
        if (!HasDirectConstructionShape(symbol))
        {
            return false;
        }

        // Exactly one instance constructor, public and parameterless. With any richer one in
        // play — a record's synthesized copy constructor included — the container's selection
        // and `new()` can part ways, dropping dependencies the container would have injected.
        return symbol.InstanceConstructors.Length == 1
               && symbol.InstanceConstructors[0] is { Parameters.IsEmpty: true, DeclaredAccessibility: Accessibility.Public };
    }

    /// <summary>
    /// Reports whether a type has the shape either construction factory needs.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns><c>true</c> when nothing about the type itself rules construction out.</returns>
    /// <remarks>
    /// A concrete, non-generic class, implementing neither <c>IDisposable</c> nor
    /// <c>IAsyncDisposable</c> — the container tracks a transient disposable in the resolving
    /// scope and direct construction would not — with no <c>required</c> member anywhere in
    /// its hierarchy, since an emitted <c>new</c> would fail to compile with CS9035 where the
    /// activation it replaces ignores them.
    /// </remarks>
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
    /// Builds the provider-taking construction factory for a handler.
    /// </summary>
    /// <param name="symbol">The handler to construct.</param>
    /// <param name="handlerTypeExpression">The expression naming the handler type.</param>
    /// <param name="currentAssembly">
    /// The compilation's assembly, or <c>null</c> for a handler read from metadata.
    /// </param>
    /// <param name="usesKeyedServices">
    /// Set when the factory resolves at least one keyed service.
    /// </param>
    /// <returns>
    /// A <c>static provider =&gt; new THandler(...)</c> lambda, or <c>null</c> when the
    /// handler does not qualify.
    /// </returns>
    /// <remarks>
    /// The bar is that the factory does what
    /// <c>Microsoft.Extensions.DependencyInjection</c> would do with the type's plain
    /// transient registration. The container only considers public constructors, so exactly
    /// one public constructor leaves it no choice, and every parameter must be a plain
    /// service resolution or a <c>[FromKeyedServices]</c> one from the very provider the
    /// container would have resolved from. Anything that makes the container's choice depend
    /// on what happens to be registered rules the factory out: an optional or default-valued
    /// parameter, several public constructors, a <c>ref</c>-like or <c>params</c> parameter,
    /// <c>[ServiceKey]</c> injection, a parameter type generated code cannot name, or a key
    /// it cannot reproduce exactly.
    /// </remarks>
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
    /// Builds the bare <c>new T(...)</c> expression for a participant.
    /// </summary>
    /// <param name="symbol">The participant to construct.</param>
    /// <param name="typeExpression">The expression naming its type.</param>
    /// <param name="currentAssembly">
    /// The compilation's assembly, or <c>null</c> for a participant read from metadata.
    /// </param>
    /// <param name="providerIdentifier">The identifier constructor dependencies resolve from.</param>
    /// <param name="allowParameterless">
    /// Whether a parameterless construction counts as an answer.
    /// </param>
    /// <param name="usesKeyedServices">
    /// Set when the expression resolves at least one keyed service.
    /// </param>
    /// <returns>
    /// The construction expression, or <c>null</c> when the participant does not qualify;
    /// see <see cref="GetProviderConstructionExpression"/> for what qualifying means.
    /// </returns>
    /// <remarks>
    /// The staged plans emit this expression directly against <c>serviceProvider</c>, while
    /// the provider factories wrap it in a lambda. A parameterless construction is produced
    /// only when asked for, because the plan factories keep those on the cheaper
    /// <c>Func&lt;THandler&gt;</c> shape.
    /// </remarks>
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
                // The container's constructor selection cannot see it, so neither does this.
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
    /// Reports whether generated code can name a closed type in a generic argument position.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="currentAssembly">
    /// The compilation's assembly, or <c>null</c> when there is none to compare against.
    /// </param>
    /// <returns><c>true</c> when the type and every one of its arguments can be named.</returns>
    /// <remarks>
    /// The name must be spellable and the whole containing chain public — internal counts
    /// only for the compilation's own types, since a referenced assembly's
    /// <c>InternalsVisibleTo</c> grant is deliberately not read here. Each generic argument
    /// is held to the same bar.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a namespace is <c>Microsoft.Extensions.DependencyInjection</c>.
    /// </summary>
    /// <param name="ns">The namespace to test.</param>
    /// <returns><c>true</c> when it is exactly that namespace.</returns>
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
    /// Writes the literal that reproduces a <c>[FromKeyedServices]</c> key.
    /// </summary>
    /// <param name="attribute">The attribute carrying the key.</param>
    /// <param name="currentAssembly">
    /// The compilation's assembly, or <c>null</c> when there is none to compare against.
    /// </param>
    /// <returns>
    /// The literal, or <c>null</c> when the key cannot be reproduced — which keeps the
    /// handler on the container path.
    /// </returns>
    /// <remarks>
    /// The container matches keys by boxed equality, so the emitted constant has to carry the
    /// same runtime type and value as the attribute's. Strings, chars, bools, integral
    /// primitives, enums and <c>typeof</c> keys can be written back; null, floating-point and
    /// array keys cannot.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a type declares more than one public instance constructor.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns><c>true</c> when a second public constructor is found.</returns>
    /// <remarks>
    /// What ERGO003 reports: with more than one, the container's greedy selection depends on
    /// what is registered, and no factory can be proven to match it.
    /// </remarks>
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

    /// <summary>
    /// Reports whether any constructor parameter carries <c>[FromServices]</c>.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <returns><c>true</c> when the attribute is found on a constructor parameter.</returns>
    /// <remarks>
    /// What ERGO004 reports: the attribute does nothing there.
    /// </remarks>
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
