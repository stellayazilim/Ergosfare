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

        if (ParticipantAttributes.IsExcludedFromDiscovery(symbol))
        {
            // Deliberately outside the closed world, but the reachability judgment has to
            // know the zone is there — so the exclusion flows through as a shadow model
            // rather than vanishing.
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

        // The informational diagnostics are about how a pipeline participant is built, so
        // only a type carrying contracts answers for them.
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
            // A handler-bearing type keeps its declaration location too: ERGO007 and ERGO008
            // anchor an unreachable handler there, and an annotated or dispatchable message
            // anchors ERGO011 through ERGO013 the same way.
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
    /// Builds models for the event messages a subscriber names but that carry no marker of
    /// their own.
    /// </summary>
    /// <param name="symbol">The type to read subscriptions from.</param>
    /// <returns>
    /// One model per such message; empty for anything that is not a subscriber, which is
    /// nearly everything.
    /// </returns>
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
    /// Builds the model of a plain type a subscriber named as its event.
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
            // This type declares no marker itself; it is a message because a subscriber named
            // it as one.
            ImplementsMessageMarker = false,
            DerivedEventMessages = DeriveEventMessages(symbol),
            MetadataSortKey = SymbolNaming.BuildMetadataName(symbol),
        };
    }

    /// <summary>
    /// Builds the reduced model of a type marked <c>[ExcludeFromDiscovery]</c>.
    /// </summary>
    /// <param name="symbol">The hidden type.</param>
    /// <param name="isCommand">Whether it reaches the command marker.</param>
    /// <param name="isQuery">Whether it reaches the query marker.</param>
    /// <param name="isEvent">Whether it reaches the event marker.</param>
    /// <param name="referencedAssemblyName">
    /// The assembly it was scanned from, or <c>null</c> when this compilation declares it.
    /// </param>
    /// <returns>A model that is never registered and never diagnosed.</returns>
    /// <remarks>
    /// It carries what the reachability judgment needs to see the exclusion zone — the
    /// assignable chain of anything that could be a message at run time, and the messages its
    /// main-handler contracts name — and what a hidden type still owes emission: its row in
    /// the frozen composition, and the roots a dispatch of it would close. What it costs to
    /// construct stays out, because nothing hidden is ever constructed from here.
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

        // Hidden from discovery, yet still part of a pipeline: [ExcludeFromDiscovery] keeps a
        // type out of bulk registration; it does not stop a handler being written for it or
        // someone registering it by hand. So the frozen table describes these types too, as
        // messages and as participants, and the consuming container's own registrations
        // decide whether the rows run. Emission names the type, which is why this needs real
        // accessibility where the judgment's exclusion zone does not.
        var isAccessible = SymbolNaming.IsAccessibleFromGeneratedCode(symbol);
        var isMessageShape = isAccessible && ContractReader.IsMessageShape(symbol, descriptors);

        // Rooting a hidden message means rooting its result contracts as well: AddMessage
        // closes the message generic while AddResult and AddStream close the (message,
        // result) ones, and those are separate tables. Without the results, a hidden
        // ICommand<string> dispatched by result would still close its generic through
        // MakeGenericType.
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
