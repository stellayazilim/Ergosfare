using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Walks referenced assemblies for constructs the consuming compilation has to register
///     — the compile-time replacement for the runtime assembly scan that used to do this. A
///     library's handlers register through its consumer's generated code, so the consumer
///     has to see them.
/// </summary>
/// <remarks>
///     Only assemblies that reference Ergosfare are inspected, because nothing else can
///     implement a marker; Ergosfare's own assemblies are skipped, because its handler
///     contract interfaces inherit the markers and would otherwise all match.
/// </remarks>

internal static class ReferenceScanner
{
    /// <summary>
    ///     Ergosfare's own assemblies are excluded from the scan: their handler contract
    ///     interfaces inherit the module markers, so every one of them would match.
    /// </summary>
    private const string ErgosfareAssemblyNamePrefix = "Stella.Ergosfare";

    // Per-assembly opt-in that force-includes an assembly matching the reserved prefix.
    // Surfaced from the ErgosfareSourceGeneratorForceScanReferences MSBuild property as
    // [assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")].
    private const string ForceScanReferencesMetadataKey = "ErgosfareSourceGeneratorForceScanReferences";
    private const string AssemblyMetadataAttributeName = "AssemblyMetadataAttribute";
    private const string AssemblyMetadataAttributeNamespace = "System.Reflection";

    /// <summary>
    ///     Discovers registrable marker types in the compilation's referenced assemblies —
    ///     the compile-time replacement for runtime scanning. Only assemblies that
    ///     themselves reference an Ergosfare assembly can contain marker types, so
    ///     everything else is skipped on a metadata-name check without realizing any of its
    ///     types; Ergosfare's own assemblies are excluded because their handler contract
    ///     interfaces inherit the module markers and must not be registered as user types.
    ///     A downstream assembly that deliberately lives under the reserved prefix can opt
    ///     back in per-assembly (see <see cref="HasForceScanReferencesOptIn"/>); the
    ///     library's own assemblies never do, so their contracts stay unregistered.
    /// </summary>
    internal static ImmutableArray<RegistrableTypeModel> ScanReferencedAssemblies(
        Compilation compilation,
        CancellationToken ct)
    {
        ImmutableArray<RegistrableTypeModel>.Builder? results = null;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            if (!ReferencesErgosfare(assembly))
            {
                continue;
            }

            // The reserved-prefix exclusion is per-assembly opt-out-able in reverse: an
            // assembly under the prefix is skipped unless it explicitly force-opts-in.
            if (IsErgosfareAssemblyName(assembly.Name) && !HasForceScanReferencesOptIn(assembly))
            {
                continue;
            }

            // A library that opted out of discovery wholesale still shapes the judgment's
            // exclusion zone: its marker types flow through as shadow models only.
            var assemblyExcluded = ParticipantAttributes.HasExcludeFromDiscovery(assembly.GetAttributes());

            var givesAccess = assembly.GivesAccessTo(compilation.Assembly);

            CollectNamespaceTypes(assembly.GlobalNamespace, assembly.Name, givesAccess, assemblyExcluded, ref results, ct);
        }

        return results?.ToImmutable() ?? ImmutableArray<RegistrableTypeModel>.Empty;
    }

    /// <summary>
    ///     Whether the assembly name is Ergosfare's own (<c>Stella.Ergosfare</c> or a
    ///     dotted child of it).
    /// </summary>
    internal static bool IsErgosfareAssemblyName(string name)
        => name.StartsWith(ErgosfareAssemblyNamePrefix, StringComparison.Ordinal)
           && (name.Length == ErgosfareAssemblyNamePrefix.Length
               || name[ErgosfareAssemblyNamePrefix.Length] == '.');

    /// <summary>
    ///     Whether the assembly force-opts back into reference scanning despite matching the
    ///     reserved <c>Stella.Ergosfare</c> name prefix. The opt-in is a per-assembly marker —
    ///     <c>[assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")]</c>,
    ///     surfaced from the same-named MSBuild property — so only assemblies that set it are
    ///     scanned. The library's own assemblies never declare it, which is what keeps their
    ///     marker-inheriting contract interfaces out of the generated registrations.
    /// </summary>
    internal static bool HasForceScanReferencesOptIn(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: AssemblyMetadataAttributeName } attributeClass
                && SymbolNaming.IsInNamespace(attributeClass, AssemblyMetadataAttributeNamespace)
                && attribute.ConstructorArguments.Length == 2
                && attribute.ConstructorArguments[0].Value is string key
                && string.Equals(key, ForceScanReferencesMetadataKey, StringComparison.Ordinal)
                && attribute.ConstructorArguments[1].Value is string value
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the assembly's metadata records a reference to any Ergosfare assembly —
    ///     a pure name check over the assembly-reference table, no symbol realization.
    /// </summary>
    internal static bool ReferencesErgosfare(IAssemblySymbol assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var reference in module.ReferencedAssemblies)
            {
                if (IsErgosfareAssemblyName(reference.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static void CollectNamespaceTypes(
        INamespaceSymbol ns,
        string assemblyName,
        bool givesAccess,
        bool assemblyExcluded,
        ref ImmutableArray<RegistrableTypeModel>.Builder? results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol nestedNamespace)
            {
                CollectNamespaceTypes(nestedNamespace, assemblyName, givesAccess, assemblyExcluded, ref results, ct);
            }
            else if (member is INamedTypeSymbol type)
            {
                CollectTypeAndNested(type, assemblyName, givesAccess, assemblyExcluded, ref results);
            }
        }
    }

    internal static void CollectTypeAndNested(
        INamedTypeSymbol type,
        string assemblyName,
        bool givesAccess,
        bool assemblyExcluded,
        ref ImmutableArray<RegistrableTypeModel>.Builder? results)
    {
        if (TryCreateReferencedModel(type, assemblyName, givesAccess, assemblyExcluded) is { } model)
        {
            (results ??= ImmutableArray.CreateBuilder<RegistrableTypeModel>()).Add(model);
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectTypeAndNested(nested, assemblyName, givesAccess, assemblyExcluded, ref results);
        }
    }

    /// <summary>
    ///     Projects a metadata type from a referenced assembly to its registration model,
    ///     or <c>null</c> when it carries no Ergosfare marker. Mirrors
    ///     <see cref="Transform"/>; descriptor computation is shared because both operate
    ///     on <see cref="INamedTypeSymbol"/>. Types the generated code cannot name —
    ///     internal without an <c>InternalsVisibleTo</c> grant, protected or private
    ///     nested, or compiler-mangled (file-local) — flow through as inaccessible and
    ///     surface as ERGOSG002.
    /// </summary>
    internal static RegistrableTypeModel? TryCreateReferencedModel(
        INamedTypeSymbol symbol,
        string assemblyName,
        bool givesAccess,
        bool assemblyExcluded)
    {
        if (symbol.IsStatic || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        ParticipantAttributes.GetMarkers(symbol, out var isCommand, out var isQuery, out var isEvent);

        if (!isCommand && !isQuery && !isEvent)
        {
            return null;
        }

        if (assemblyExcluded || ParticipantAttributes.IsExcludedFromDiscovery(symbol))
        {
            // Same shadow posture as source-declared exclusions; see Transform.
            return RegistrableTypeReader.CreateExcludedShadowModel(symbol, isCommand, isQuery, isEvent, assemblyName);
        }

        var isAccessible = IsVisibleToCompilation(symbol, givesAccess) && SymbolNaming.HasSpellableName(symbol);
        var descriptors = isAccessible ? ContractReader.BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && ContractReader.IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);
        var typeofExpression = SymbolNaming.BuildTypeofExpression(symbol);
        var dispatchResults = isDispatchable ? ContractReader.GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty;

        // Referenced adapters get no current-assembly grant either: baking qualifies only
        // over fully public adapter types. ERGOSG011/012 never fire here (the annotations
        // were judged where the message was compiled); the model only feeds the plan binding.
        var referencedHasIgnore = false;
        var resultAdapter = isDispatchable
            ? ResultAdapterReader.GetResultAdapterModel(symbol, dispatchResults, isCommand, currentAssembly: null, out referencedHasIgnore)
            : null;

        // Referenced handlers get no current-assembly grant: their construction factory
        // qualifies only over fully public parameter types (IVT grants are not modeled).
        var usesKeyedServices = false;
        var providerConstruction = isAccessible
            ? ConstructionAnalyzer.GetProviderConstructionExpression(symbol, typeofExpression, currentAssembly: null, out usesKeyedServices)
            : null;

        var referencedStagedKeyedServices = false;
        var referencedStagedConstruction = isAccessible && !descriptors.IsEmpty
            ? ConstructionAnalyzer.TryBuildConstructionExpression(symbol, typeofExpression, currentAssembly: null,
                "serviceProvider", allowParameterless: true, out referencedStagedKeyedServices)
            : null;

        return new RegistrableTypeModel
        {
            TypeofExpression = typeofExpression,
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = isAccessible,
            Location = null,
            Weight = ParticipantAttributes.GetWeight(symbol),
            GroupsExpression = ParticipantAttributes.GetGroupsExpression(symbol),
            GroupNames = ParticipantAttributes.GetGroupNames(symbol),
            Descriptors = descriptors,
            ReferencedAssemblyName = assemblyName,
            DiscoveryKeys = ParticipantAttributes.GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            IsMessageShape = isMessageShape,
            DispatchResults = dispatchResults,
            IsDirectlyConstructible = isAccessible && ConstructionAnalyzer.IsDirectlyConstructible(symbol),
            ProviderConstructionExpression = providerConstruction,
            ProviderConstructionUsesKeyedServices = usesKeyedServices,
            HasPipelineExclusion = ParticipantAttributes.HasPipelineExclusionAttribute(symbol),
            ExcludedInterceptorGroups = ParticipantAttributes.GetPipelineExclusionGroups(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            IsGenericParticipant = Monomorphizer.IsUnbindableGenericParticipant(symbol),
            AssignableKeys = isMessageShape ? ParticipantAttributes.GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = isAccessible ? ContractReader.BuildContractShapes(symbol) : ImmutableArray<ContractShapeModel>.Empty,
            StagedConstructionExpression = referencedStagedConstruction,
            StagedConstructionUsesKeyedServices = referencedStagedKeyedServices,
            HasMultiplePublicConstructors = false,
            HasFromServicesConstructorParameter = false,
            InfoLocation = null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = resultAdapter,
            HasIgnoredResultAdapter = referencedHasIgnore,
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

    /// <summary>
    ///     Whether generated code in the current compilation can name a type declared in a
    ///     referenced assembly: every level of the containing-type chain must be public, or
    ///     internal with the assembly granting this compilation
    ///     <c>InternalsVisibleTo</c> access.
    /// </summary>
    internal static bool IsVisibleToCompilation(INamedTypeSymbol symbol, bool givesAccess)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    break;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (!givesAccess)
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
