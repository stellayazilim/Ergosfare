using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Walks referenced assemblies for the constructs the consuming compilation has to register.
/// </summary>
/// <remarks>
/// A library's handlers register through its consumer's generated code, so the consumer has
/// to see them. Only assemblies that reference Ergosfare are inspected, because nothing else
/// can carry a marker; Ergosfare's own are skipped, because its contract interfaces inherit
/// the markers and would otherwise all match.
/// </remarks>
internal static class ReferenceScanner
{
    /// <summary>
    /// The assembly-name prefix reserved for Ergosfare's own assemblies.
    /// </summary>
    private const string ErgosfareAssemblyNamePrefix = "Stella.Ergosfare";

    // The per-assembly opt-in that scans an assembly matching the reserved prefix anyway,
    // written as [assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences",
    // "true")] and surfaced from the MSBuild property of the same name.
    private const string ForceScanReferencesMetadataKey = "ErgosfareSourceGeneratorForceScanReferences";
    private const string AssemblyMetadataAttributeName = "AssemblyMetadataAttribute";
    private const string AssemblyMetadataAttributeNamespace = "System.Reflection";

    /// <summary>
    /// Finds the registrable marker types in a compilation's referenced assemblies.
    /// </summary>
    /// <param name="compilation">The compilation whose references are scanned.</param>
    /// <param name="ct">Cancels the scan.</param>
    /// <returns>One model per marker type found.</returns>
    /// <remarks>
    /// An assembly that references no Ergosfare assembly can hold no marker type, so it is
    /// dismissed on a name check without realizing a single symbol. Ergosfare's own
    /// assemblies are left out so their contract interfaces are never registered as user
    /// types; an assembly that deliberately lives under the reserved prefix can opt back in
    /// through <see cref="HasForceScanReferencesOptIn"/>, which the library's own never do.
    /// </remarks>
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

            // The reserved prefix keeps an assembly out unless that assembly asks to be let
            // back in.
            if (IsErgosfareAssemblyName(assembly.Name) && !HasForceScanReferencesOptIn(assembly))
            {
                continue;
            }

            // A library that opted out of discovery wholesale still shapes the judgment's
            // exclusion zone, so its marker types flow through as shadow models only.
            var assemblyExcluded = ParticipantAttributes.HasExcludeFromDiscovery(assembly.GetAttributes());

            var givesAccess = assembly.GivesAccessTo(compilation.Assembly);

            CollectNamespaceTypes(assembly.GlobalNamespace, assembly.Name, givesAccess, assemblyExcluded, ref results, ct);
        }

        return results?.ToImmutable() ?? ImmutableArray<RegistrableTypeModel>.Empty;
    }

    /// <summary>
    /// Reports whether an assembly name belongs to Ergosfare itself.
    /// </summary>
    /// <param name="name">The assembly name to test.</param>
    /// <returns>
    /// <c>true</c> for <c>Stella.Ergosfare</c> and any dotted child of it.
    /// </returns>
    internal static bool IsErgosfareAssemblyName(string name)
        => name.StartsWith(ErgosfareAssemblyNamePrefix, StringComparison.Ordinal)
           && (name.Length == ErgosfareAssemblyNamePrefix.Length
               || name[ErgosfareAssemblyNamePrefix.Length] == '.');

    /// <summary>
    /// Reports whether an assembly asks to be scanned despite carrying the reserved name
    /// prefix.
    /// </summary>
    /// <param name="assembly">The assembly to test.</param>
    /// <returns><c>true</c> when it declares the opt-in.</returns>
    /// <remarks>
    /// The opt-in is
    /// <c>[assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")]</c>,
    /// surfaced from the MSBuild property of the same name. Only an assembly that sets it is
    /// scanned; the library's own never do, which is what keeps their marker-inheriting
    /// contract interfaces out of the generated registrations.
    /// </remarks>
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
    /// Reports whether an assembly's metadata records a reference to any Ergosfare assembly.
    /// </summary>
    /// <param name="assembly">The assembly to test.</param>
    /// <returns><c>true</c> when it references one.</returns>
    /// <remarks>
    /// A name check over the assembly-reference table; no symbol is realized to answer it.
    /// </remarks>
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

    /// <summary>
    /// Walks a namespace and everything nested under it, collecting the marker types.
    /// </summary>
    /// <param name="ns">The namespace to walk.</param>
    /// <param name="assemblyName">The assembly the namespace belongs to.</param>
    /// <param name="givesAccess">Whether that assembly grants this compilation access to its internals.</param>
    /// <param name="assemblyExcluded">Whether that assembly opted out of discovery as a whole.</param>
    /// <param name="results">The builder collected models are added to; created on first use.</param>
    /// <param name="ct">Cancels the walk.</param>
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

    /// <summary>
    /// Collects a type and every type nested inside it.
    /// </summary>
    /// <param name="type">The type to collect.</param>
    /// <param name="assemblyName">The assembly the type belongs to.</param>
    /// <param name="givesAccess">Whether that assembly grants this compilation access to its internals.</param>
    /// <param name="assemblyExcluded">Whether that assembly opted out of discovery as a whole.</param>
    /// <param name="results">The builder collected models are added to; created on first use.</param>
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
    /// Reads one metadata type from a referenced assembly into its model.
    /// </summary>
    /// <param name="symbol">The type to read.</param>
    /// <param name="assemblyName">The assembly it was found in.</param>
    /// <param name="givesAccess">Whether that assembly grants this compilation access to its internals.</param>
    /// <param name="assemblyExcluded">Whether that assembly opted out of discovery as a whole.</param>
    /// <returns>The model, or <c>null</c> when the type carries no Ergosfare marker.</returns>
    /// <remarks>
    /// The metadata counterpart of <see cref="RegistrableTypeReader.Transform"/>, sharing its
    /// contract reading because both work from an <see cref="INamedTypeSymbol"/>. A type
    /// generated code cannot name — internal without a grant, protected or private nested, or
    /// compiler-mangled — comes back inaccessible and is reported as ERGO002.
    /// </remarks>
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
            // The same posture a source-declared exclusion takes; see Transform.
            return RegistrableTypeReader.CreateExcludedShadowModel(symbol, isCommand, isQuery, isEvent, assemblyName);
        }

        var isAccessible = IsVisibleToCompilation(symbol, givesAccess) && SymbolNaming.HasSpellableName(symbol);
        var descriptors = isAccessible ? ContractReader.BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && ContractReader.IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);
        var typeofExpression = SymbolNaming.BuildTypeofExpression(symbol);
        var dispatchResults = isDispatchable ? ContractReader.GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty;

        // A referenced adapter is read without a current-assembly grant, so baking qualifies
        // over fully public adapter types only. ERGO011 and ERGO012 never fire from here —
        // the annotations were answered for where the message was compiled — and the model
        // feeds nothing but the plan binding.
        var referencedHasIgnore = false;
        var resultAdapter = isDispatchable
            ? ResultAdapterReader.GetResultAdapterModel(symbol, dispatchResults, isCommand, currentAssembly: null, out referencedHasIgnore)
            : null;

        // A referenced handler likewise: its construction factory qualifies over fully public
        // parameter types, because an InternalsVisibleTo grant is not modeled here.
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
            ImplementsMessageMarker = true,
            DerivedEventMessages = ImmutableArray<RegistrableTypeModel>.Empty,
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

    /// <summary>
    /// Reports whether generated code in this compilation can name a type from a referenced
    /// assembly.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <param name="givesAccess">
    /// Whether the declaring assembly grants this compilation access to its internals.
    /// </param>
    /// <returns><c>true</c> when every level of the type's containing chain is reachable.</returns>
    /// <remarks>
    /// Each level must be public, or internal with the grant in place.
    /// </remarks>
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
