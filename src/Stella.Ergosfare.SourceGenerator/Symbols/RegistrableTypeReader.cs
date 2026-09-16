using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Projects a declared type into the model the rest of the generator works from.
/// </summary>
/// <remarks>
/// The only place a <see cref="GeneratorSyntaxContext"/> becomes a
/// <see cref="RegistrableTypeModel"/>. Everything downstream sees values rather than
/// symbols, which is what lets the incremental pipeline compare models and skip declarations
/// that did not change.
/// </remarks>
internal static class RegistrableTypeReader
{
    /// <summary>
    /// Reads one candidate type declaration.
    /// </summary>
    /// <param name="ctx">The declaration to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>
    /// The type's model, or <c>null</c> when it carries no Ergosfare marker.
    /// </returns>
    /// <remarks>
    /// Runs once per declaration, so a partial type yields a model per part; the registration
    /// pipeline drops the repeats.
    /// </remarks>
    internal static RegistrableTypeModel? Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node, ct) is not { } symbol)
        {
            return null;
        }

        // A static class cannot implement an interface, and an implicitly declared symbol is
        // a compiler artifact. Neither is registrable.
        if (symbol.IsStatic || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        ParticipantAttributes.GetMarkers(symbol, out var isCommand, out var isQuery, out var isEvent);

        if (!isCommand && !isQuery && !isEvent)
        {
            return null;
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

        // The informational diagnostics are about how a pipeline participant is built, so
        // only a type carrying contracts answers for them.
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
            CanRegisterParticipant = isAccessible && ConstructionAnalyzer.CanRegisterParticipant(symbol),
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
            HasFromServicesConstructorParameter = hasFromServices,
            // A handler-bearing type keeps its declaration location too: ERGO007 and ERGO008
            // anchor an unreachable handler there, and an annotated or dispatchable message
            // anchors ERGO011 through ERGO013 the same way.
            InfoLocation = hasFromServices || !descriptors.IsEmpty
                           || resultAdapter is not null || isDispatchable
                ? LocationInfo.From(symbol)
                : null,
            IsExcludedFromDiscovery = ParticipantAttributes.IsExcludedFromDiscovery(symbol),
            ResultAdapter = resultAdapter,
            HasIgnoredResultAdapter = hasIgnoredResultAdapter,
            ImplementsMessageMarker = true,
            DerivedMessages = DeriveMessages(symbol),
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

    /// <summary>
    /// Derives closed generic messages and unmarked events from main-handler contracts.
    /// </summary>
    /// <param name="symbol">The type to read subscriptions from.</param>
    /// <returns>
    /// One model per such message; empty for anything that is not a subscriber, which is
    /// nearly everything.
    /// </returns>
    internal static ImmutableArray<RegistrableTypeModel> DeriveMessages(INamedTypeSymbol symbol)
    {
        var messages = ContractReader.GetDerivedMessages(symbol);

        if (messages.IsEmpty)
        {
            return ImmutableArray<RegistrableTypeModel>.Empty;
        }

        var models = ImmutableArray.CreateBuilder<RegistrableTypeModel>(messages.Length);

        foreach (var message in messages)
        {
            models.Add(CreateDerivedMessageModel(message));
        }

        return models.ToImmutable();
    }

    /// <summary>
    /// Builds a message model from the concrete type named by a main handler.
    /// </summary>
    /// <param name="symbol">The named type.</param>
    /// <returns>A model describing a message and nothing else.</returns>
    /// <remarks>
    /// It carries no contracts, is never constructed by the pipeline, and answers no
    /// diagnostics of its own — the subscriber that named it is where those belong.
    /// <see cref="RegistrableTypeModel.ReferencedAssemblyName"/> stays <c>null</c> even for a
    /// type declared elsewhere: that field records where scanning found a model, and this one
    /// was not found but created, because this compilation declares a subscriber for it.
    /// </remarks>
    private static RegistrableTypeModel CreateDerivedMessageModel(INamedTypeSymbol symbol)
    {
        ParticipantAttributes.GetMarkers(symbol, out var isCommand, out var isQuery, out var isEvent);
        var hasMarker = isCommand || isQuery || isEvent;
        var isAccessible = SymbolNaming.IsAccessibleFromGeneratedCode(symbol);
        var descriptors = ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && ContractReader.IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);
        var declaredHere = symbol.DeclaringSyntaxReferences.Length > 0;
        var dispatchResults = isDispatchable ? ContractReader.GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty;
        var hasIgnoredResultAdapter = false;
        var resultAdapter = isDispatchable
            ? ResultAdapterReader.GetResultAdapterModel(symbol, dispatchResults, isCommand, symbol.ContainingAssembly, out hasIgnoredResultAdapter)
            : null;

        return new RegistrableTypeModel
        {
            TypeofExpression = SymbolNaming.BuildTypeofExpression(symbol),
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent || !hasMarker,
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
            DispatchResults = dispatchResults,
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
            HasFromServicesConstructorParameter = false,
            InfoLocation = isDispatchable && declaredHere ? LocationInfo.From(symbol) : null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = resultAdapter,
            HasIgnoredResultAdapter = hasIgnoredResultAdapter,
            // This type declares no marker itself; it is a message because a subscriber named
            // it as one.
            ImplementsMessageMarker = hasMarker,
            DerivedMessages = DeriveMessages(symbol),
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

}
