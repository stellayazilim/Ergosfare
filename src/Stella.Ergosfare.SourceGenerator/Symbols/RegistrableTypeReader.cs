using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Projects one declared type into the model the rest of the generator works from. This
///     is the only place a <c>GeneratorSyntaxContext</c> is turned into a
///     <see cref="RegistrableTypeModel"/>, which is what keeps the model comparable by value
///     and the incremental pipeline able to skip unchanged declarations.
/// </summary>

internal static class RegistrableTypeReader
{
    /// <summary>
    ///     Projects a candidate type declaration to its registration model, or <c>null</c>
    ///     when the type carries no Ergosfare marker. Runs per declaration; partial types
    ///     may yield duplicates, which the registration pipeline dedupes.
    /// </summary>
    internal static RegistrableTypeModel? Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node, ct) is not { } symbol)
        {
            return null;
        }

        // Static classes cannot implement interfaces; implicitly declared symbols are
        // compiler artifacts. Neither is registrable.
        if (symbol.IsStatic || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        ParticipantAttributes.GetMarkers(symbol, out var isCommand, out var isQuery, out var isEvent);

        if (!isCommand && !isQuery && !isEvent)
        {
            return null;
        }

        if (ParticipantAttributes.IsExcludedFromDiscovery(symbol))
        {
            // Deliberately outside the closed world — but the reachability judgment must
            // know the zone exists, so the exclusion flows through as a shadow model
            // instead of vanishing.
            return CreateExcludedShadowModel(symbol, isCommand, isQuery, isEvent, referencedAssemblyName: null);
        }

        var isAccessible = SymbolNaming.IsAccessibleFromGeneratedCode(symbol);
        var descriptors = isAccessible ? ContractReader.BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && ContractReader.IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);
        var typeofExpression = SymbolNaming.BuildTypeofExpression(symbol);
        var dispatchResults = isDispatchable ? ContractReader.GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty;
        var hasIgnoredResultAdapter = false;
        var resultAdapter = isDispatchable
            ? ResultAdapterReader.GetResultAdapterModel(symbol, dispatchResults, isCommand, symbol.ContainingAssembly, out hasIgnoredResultAdapter)
            : null;

        var usesKeyedServices = false;
        var providerConstruction = isAccessible
            ? ConstructionAnalyzer.GetProviderConstructionExpression(symbol, typeofExpression, symbol.ContainingAssembly, out usesKeyedServices)
            : null;

        // Informational diagnostics apply to pipeline participants declared in source —
        // the only place the user can act on them.
        var hasMultipleCtors = !descriptors.IsEmpty && ConstructionAnalyzer.HasMultiplePublicInstanceConstructors(symbol);
        var hasFromServices = !descriptors.IsEmpty && ConstructionAnalyzer.HasFromServicesOnConstructor(symbol);

        var stagedKeyedServices = false;
        var stagedConstruction = isAccessible && !descriptors.IsEmpty
            ? ConstructionAnalyzer.TryBuildConstructionExpression(symbol, typeofExpression, symbol.ContainingAssembly,
                "serviceProvider", allowParameterless: true, out stagedKeyedServices)
            : null;

        return new RegistrableTypeModel
        {
            TypeofExpression = typeofExpression,
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = isAccessible,
            Location = isAccessible ? null : LocationInfo.From(symbol),
            Weight = ParticipantAttributes.GetWeight(symbol),
            GroupsExpression = ParticipantAttributes.GetGroupsExpression(symbol),
            GroupNames = ParticipantAttributes.GetGroupNames(symbol),
            Descriptors = descriptors,
            ReferencedAssemblyName = null,
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
            StagedConstructionExpression = stagedConstruction,
            StagedConstructionUsesKeyedServices = stagedKeyedServices,
            HasMultiplePublicConstructors = hasMultipleCtors,
            HasFromServicesConstructorParameter = hasFromServices,
            // Handler-bearing types keep their declaration location too: the
            // unreachable-handler diagnostics (ERGO007/008) anchor there; annotated
            // messages anchor ERGO011/012 and dispatchable ones ERGO013 the same way.
            InfoLocation = hasMultipleCtors || hasFromServices || !descriptors.IsEmpty
                           || resultAdapter is not null || isDispatchable
                ? LocationInfo.From(symbol)
                : null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = resultAdapter,
            HasIgnoredResultAdapter = hasIgnoredResultAdapter,
            ImplementsMessageMarker = true,
            DerivedEventMessages = DeriveEventMessages(symbol),
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

    /// <summary>
    ///     The models of the event messages this type subscribes to, for the ones that carry
    ///     no marker of their own. Empty for everything that is not a subscriber, which is
    ///     almost everything.
    /// </summary>
    private static ImmutableArray<RegistrableTypeModel> DeriveEventMessages(INamedTypeSymbol symbol)
    {
        var messages = ContractReader.GetDerivedEventMessages(symbol);

        if (messages.IsEmpty)
        {
            return ImmutableArray<RegistrableTypeModel>.Empty;
        }

        var models = ImmutableArray.CreateBuilder<RegistrableTypeModel>(messages.Length);

        foreach (var message in messages)
        {
            models.Add(CreateDerivedEventMessageModel(message));
        }

        return models.ToImmutable();
    }

    /// <summary>
    ///     The model of a plain type a subscriber named as its event. A message and nothing
    ///     else: it carries no contracts, is never constructed by the pipeline, and answers
    ///     no diagnostics of its own — the subscriber that named it is where those belong.
    /// </summary>
    /// <remarks>
    ///     <see cref="RegistrableTypeModel.ReferencedAssemblyName"/> stays <c>null</c> even
    ///     when the type is declared elsewhere. The field records where a model was
    ///     <em>found</em> by scanning, and this one was not found — it was created because
    ///     this compilation declares a subscriber for it, which is a fact about this
    ///     compilation rather than about the assembly the type happens to live in.
    /// </remarks>
    private static RegistrableTypeModel CreateDerivedEventMessageModel(INamedTypeSymbol symbol)
    {
        var isAccessible = SymbolNaming.IsAccessibleFromGeneratedCode(symbol);
        var descriptors = ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && ContractReader.IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);
        var declaredHere = symbol.DeclaringSyntaxReferences.Length > 0;

        return new RegistrableTypeModel
        {
            TypeofExpression = SymbolNaming.BuildTypeofExpression(symbol),
            DisplayName = symbol.ToDisplayString(),
            IsCommand = false,
            IsQuery = false,
            IsEvent = true,
            IsAccessible = isAccessible,
            Location = !isAccessible && declaredHere ? LocationInfo.From(symbol) : null,
            Weight = ParticipantAttributes.GetWeight(symbol),
            GroupsExpression = ParticipantAttributes.GetGroupsExpression(symbol),
            GroupNames = ParticipantAttributes.GetGroupNames(symbol),
            Descriptors = descriptors,
            ReferencedAssemblyName = null,
            DiscoveryKeys = ImmutableArray<string>.Empty,
            IsDispatchableMessage = isDispatchable,
            IsMessageShape = isMessageShape,
            DispatchResults = ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = false,
            ProviderConstructionExpression = null,
            ProviderConstructionUsesKeyedServices = false,
            HasPipelineExclusion = ParticipantAttributes.HasPipelineExclusionAttribute(symbol),
            ExcludedInterceptorGroups = ParticipantAttributes.GetPipelineExclusionGroups(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            IsGenericParticipant = false,
            AssignableKeys = isMessageShape ? ParticipantAttributes.GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = ImmutableArray<ContractShapeModel>.Empty,
            StagedConstructionExpression = null,
            StagedConstructionUsesKeyedServices = false,
            HasMultiplePublicConstructors = false,
            HasFromServicesConstructorParameter = false,
            InfoLocation = isDispatchable && declaredHere ? LocationInfo.From(symbol) : null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = null,
            HasIgnoredResultAdapter = false,
            // Hiding a subscriber from bulk collection does not stop it from running, so the
            // message it names is still a message.
            ImplementsMessageMarker = false,
            DerivedEventMessages = DeriveEventMessages(symbol),
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

    /// <summary>
    ///     The reduced model of an <c>[ExcludeFromDiscovery]</c> type: the reachability
    ///     judgment's exclusion zone — the type's assignable chain when it could be a runtime
    ///     message instance, and its main-handler descriptor messages when it carries handler
    ///     contracts — plus what a hidden participant still contributes to emission: its
    ///     pipeline row in the frozen composition, and its dispatch roots when a registration
    ///     reaches it. Never registered, never diagnosed.
    /// </summary>
    /// <remarks>
    ///     Reduced is not the same as empty, and the difference is decided per field by who
    ///     reads it. What the type would cost to construct stays out, because nothing hidden
    ///     is ever constructed from here; what a dispatch needs to close its generics stays
    ///     in, because hiding a type from bulk registration does not stop it from being
    ///     dispatched.
    /// </remarks>
    internal static RegistrableTypeModel CreateExcludedShadowModel(
        INamedTypeSymbol symbol,
        bool isCommand,
        bool isQuery,
        bool isEvent,
        string? referencedAssemblyName)
    {
        var descriptors = ContractReader.BuildDescriptors(symbol);
        var isDispatchable = ContractReader.IsDispatchableMessage(symbol, descriptors);

        // Hidden from discovery, but still part of a pipeline: [ExcludeFromDiscovery]
        // keeps a type out of bulk registration, it does not stop a handler from being
        // written for it or someone registering it by hand. The frozen table therefore
        // describes these types too — as messages and as participants — and the consuming
        // container's own registrations decide whether the rows run. Emission names the
        // type, so unlike the judgment's exclusion zone this needs real accessibility.
        var isAccessible = SymbolNaming.IsAccessibleFromGeneratedCode(symbol);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);

        // A hidden message a registration reaches is rooted, and rooting a message means its
        // result contracts too — AddMessage closes the message generic, AddResult/AddStream
        // close the (message, result) ones, and they are different tables. Left empty, the
        // shared root emission wrote the message root and silently skipped the others, so a
        // hidden ICommand<string> dispatched by result still closed its generic through
        // MakeGenericType. The judgment this model was first written for never read them.
        var dispatchResults = isDispatchable
            ? ContractReader.GetDispatchResults(symbol)
            : ImmutableArray<DispatchResultModel>.Empty;

        return new RegistrableTypeModel
        {
            TypeofExpression = SymbolNaming.BuildTypeofExpression(symbol),
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
            ReferencedAssemblyName = referencedAssemblyName,
            DiscoveryKeys = ImmutableArray<string>.Empty,
            IsDispatchableMessage = isDispatchable,
            IsMessageShape = isMessageShape,
            DispatchResults = dispatchResults,
            IsDirectlyConstructible = false,
            ProviderConstructionExpression = null,
            ProviderConstructionUsesKeyedServices = false,
            HasPipelineExclusion = ParticipantAttributes.HasPipelineExclusionAttribute(symbol),
            ExcludedInterceptorGroups = ParticipantAttributes.GetPipelineExclusionGroups(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            IsGenericParticipant = Monomorphizer.IsUnbindableGenericParticipant(symbol),
            AssignableKeys = isMessageShape ? ParticipantAttributes.GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = ImmutableArray<ContractShapeModel>.Empty,
            StagedConstructionExpression = null,
            StagedConstructionUsesKeyedServices = false,
            HasMultiplePublicConstructors = false,
            HasFromServicesConstructorParameter = false,
            InfoLocation = null,
            IsExcludedFromDiscovery = true,
            ResultAdapter = null,
            HasIgnoredResultAdapter = false,
            ImplementsMessageMarker = true,
            DerivedEventMessages = ImmutableArray<RegistrableTypeModel>.Empty,
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }
}
