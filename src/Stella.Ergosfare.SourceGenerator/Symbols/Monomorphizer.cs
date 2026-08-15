using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Closes open participants over the messages their constraints admit. A handler taking
///     its message as a type parameter is one declaration and many pipelines; monomorphizing
///     it here turns each of those pipelines into an ordinary registrable type, which is what
///     lets them be planned instead of resolved through an open generic at run time.
/// </summary>

internal static class Monomorphizer
{
    /// <summary>
    ///     Whether generated code — a sibling top-level type in the same assembly — can
    ///     reference the type. Private/protected members of other types and file-local
    ///     types cannot be named from the generated file.
    /// </summary>
    /// <summary>
    ///     Whether a generic participant is one no message can bind.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     A generic participant binds when its contract's message type is built from its
    ///     own type parameters — <c>WrapHandler&lt;T&gt; : ICommandHandler&lt;Wrap&lt;T&gt;&gt;</c>.
    ///     The table then keys the message by its definition and names the participant by
    ///     its unbound <c>typeof</c>, and the dispatch closes the participant over the
    ///     dispatched message's own generic arguments (<c>FrozenComposition.Close</c>). One
    ///     baked entry serves every instantiation.
    ///     </para>
    ///     <para>
    ///     It binds to nothing when the contract's message type is the type parameter
    ///     itself — <c>ValidateCommands&lt;TCommand&gt; : ICommandPreInterceptor&lt;TCommand&gt;</c>.
    ///     The message is then any concrete command, which carries no generic arguments to
    ///     close the participant over, and participants are matched to messages by concrete
    ///     type, so no message's stage arrays ever contain it.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     Whether a generic level is still open — the definition itself, or a constructed
    ///     form whose arguments are its own type parameters. A form closed over concrete
    ///     types is neither, and can be named.
    /// </summary>
    internal static bool IsUnboundOrDefinition(INamedTypeSymbol type)
    {
        if (type.IsUnboundGenericType || SymbolEqualityComparer.Default.Equals(type, type.OriginalDefinition))
        {
            return true;
        }

        foreach (var argument in type.TypeArguments)
        {
            if (argument is ITypeParameterSymbol)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsUnbindableGenericParticipant(INamedTypeSymbol symbol)
    {
        if (symbol.Arity == 0)
        {
            return false;
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity is not (1 or 2) || !SymbolNaming.IsInNamespace(iface, ContractNames.HandlerNamespace))
            {
                continue;
            }

            if (iface.TypeArguments[0] is ITypeParameterSymbol)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Closes every unbindable open participant over the messages its constraint admits,
    ///     one closed model per pair — the compile-time counterpart of the instantiations a
    ///     generic method gets in the binary.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The closing set does not come from the source: nobody writes
    ///     <c>ValidateCommands&lt;RegisterUser&gt;</c>. It comes from the type parameter's
    ///     constraint intersected with the compiled message set, and every pair that
    ///     survives becomes a distinct type with its own registration and its own place in
    ///     that message's pipeline.
    ///     </para>
    ///     <para>
    ///     A participant that closes over nothing keeps ERGOSG016: it was registered and it
    ///     still runs for no message.
    ///     </para>
    /// </remarks>
    internal static ImmutableArray<RegistrableTypeModel> MonomorphizeOpenParticipants(
        Compilation compilation,
        CancellationToken ct)
    {
        List<INamedTypeSymbol>? openParticipants = null;
        List<INamedTypeSymbol>? messages = null;

        CollectMonomorphizationCandidates(
            compilation.Assembly.GlobalNamespace, ref openParticipants, ref messages, ct);

        if (openParticipants is null || messages is null)
        {
            return ImmutableArray<RegistrableTypeModel>.Empty;
        }

        ImmutableArray<RegistrableTypeModel>.Builder? results = null;

        foreach (var participant in openParticipants)
        {
            ct.ThrowIfCancellationRequested();

            // Only the single-parameter shape is modeled: the message is the one thing a
            // constraint can name, and a second parameter has nothing to be closed from.
            if (participant.Arity != 1)
            {
                continue;
            }

            var parameter = participant.TypeParameters[0];

            foreach (var message in messages)
            {
                if (!SatisfiesConstraints(parameter, message))
                {
                    continue;
                }

                var closed = participant.OriginalDefinition.Construct(message);

                if (!ConstructionAnalyzer.IsNameableClosedType(closed, compilation.Assembly))
                {
                    continue;
                }

                var model = CreateMonomorphizedModel(closed, participant, compilation.Assembly);

                if (model is { } value)
                {
                    (results ??= ImmutableArray.CreateBuilder<RegistrableTypeModel>()).Add(value);
                }
            }
        }

        return results?.ToImmutable() ?? ImmutableArray<RegistrableTypeModel>.Empty;
    }

    /// <summary>
    ///     Walks the compilation's own types for the two halves monomorphization needs: the
    ///     open participants that bind to nothing, and the messages a constraint can admit.
    /// </summary>
    internal static void CollectMonomorphizationCandidates(
        INamespaceSymbol ns,
        ref List<INamedTypeSymbol>? openParticipants,
        ref List<INamedTypeSymbol>? messages,
        CancellationToken ct)
    {
        foreach (var member in ns.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            switch (member)
            {
                case INamespaceSymbol nested:
                    CollectMonomorphizationCandidates(nested, ref openParticipants, ref messages, ct);
                    continue;
                case INamedTypeSymbol type:
                    CollectMonomorphizationCandidate(type, ref openParticipants, ref messages);
                    continue;
            }
        }
    }

    internal static void CollectMonomorphizationCandidate(
        INamedTypeSymbol type,
        ref List<INamedTypeSymbol>? openParticipants,
        ref List<INamedTypeSymbol>? messages)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            CollectMonomorphizationCandidate(nested, ref openParticipants, ref messages);
        }

        if (type.IsStatic || type.IsImplicitlyDeclared || ParticipantAttributes.IsExcludedFromDiscovery(type))
        {
            return;
        }

        ParticipantAttributes.GetMarkers(type, out var isCommand, out var isQuery, out var isEvent);

        if (!isCommand && !isQuery && !isEvent)
        {
            return;
        }

        if (IsUnbindableGenericParticipant(type))
        {
            (openParticipants ??= []).Add(type);
            return;
        }

        // A message is what a constraint can admit: dispatchable, and therefore not itself
        // a participant.
        if (ContractReader.IsDispatchableMessage(type, ContractReader.BuildDescriptors(type)))
        {
            (messages ??= []).Add(type);
        }
    }

    /// <summary>
    ///     Whether a message satisfies every constraint the participant's type parameter
    ///     declares — the compile-time question "would <c>Participant&lt;Message&gt;</c>
    ///     have compiled if someone had written it".
    /// </summary>
    internal static bool SatisfiesConstraints(ITypeParameterSymbol parameter, INamedTypeSymbol candidate)
    {
        if (parameter.HasReferenceTypeConstraint && candidate.IsValueType)
        {
            return false;
        }

        if (parameter.HasValueTypeConstraint && !candidate.IsValueType)
        {
            return false;
        }

        if (parameter.HasConstructorConstraint
            && !candidate.InstanceConstructors.Any(c =>
                c is { Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public }))
        {
            return false;
        }

        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (constraint is not INamedTypeSymbol named || !IsAssignableToConstraint(candidate, named))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsAssignableToConstraint(INamedTypeSymbol candidate, INamedTypeSymbol constraint)
    {
        for (var current = candidate; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, constraint))
            {
                return true;
            }
        }

        foreach (var iface in candidate.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, constraint))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Projects one monomorphized participant to its registration model. The closed form
    ///     is a distinct type — it names itself in full rather than in the unbound form a
    ///     declared generic uses — and its contracts now name a concrete message, which is
    ///     what lets the composition table bind it like any other participant.
    /// </summary>
    internal static RegistrableTypeModel? CreateMonomorphizedModel(
        INamedTypeSymbol closed,
        INamedTypeSymbol openDefinition,
        IAssemblySymbol currentAssembly)
    {
        var descriptors = ContractReader.BuildDescriptors(closed);

        if (descriptors.IsEmpty)
        {
            return null;
        }

        ParticipantAttributes.GetMarkers(openDefinition, out var isCommand, out var isQuery, out var isEvent);

        var usesKeyedServices = false;
        var typeofExpression = SymbolNaming.VerbatimTypeExpression(closed);
        var providerConstruction =
            ConstructionAnalyzer.GetProviderConstructionExpression(closed, typeofExpression, currentAssembly, out usesKeyedServices);

        var stagedKeyedServices = false;
        var stagedConstruction = ConstructionAnalyzer.TryBuildConstructionExpression(
            closed, typeofExpression, currentAssembly, "serviceProvider",
            allowParameterless: true, out stagedKeyedServices);

        return new RegistrableTypeModel
        {
            TypeofExpression = typeofExpression,
            DisplayName = closed.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = true,
            Location = null,
            Weight = ParticipantAttributes.GetWeight(openDefinition),
            GroupsExpression = ParticipantAttributes.GetGroupsExpression(openDefinition),
            GroupNames = ParticipantAttributes.GetGroupNames(openDefinition),
            Descriptors = descriptors,
            ReferencedAssemblyName = null,
            DiscoveryKeys = ParticipantAttributes.GetDiscoveryKeys(openDefinition),
            IsDispatchableMessage = false,
            IsMessageShape = false,
            DispatchResults = ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = ConstructionAnalyzer.IsDirectlyConstructible(closed),
            ProviderConstructionExpression = providerConstruction,
            ProviderConstructionUsesKeyedServices = usesKeyedServices,
            HasPipelineExclusion = ParticipantAttributes.HasPipelineExclusionAttribute(openDefinition),
            ExcludedInterceptorGroups = ParticipantAttributes.GetPipelineExclusionGroups(openDefinition),
            IsValueType = closed.IsValueType,
            IsNestedType = closed.ContainingType is not null,
            // Bound, so no longer the shape ERGOSG016 reports.
            IsGenericParticipant = false,
            MonomorphizedFrom = SymbolNaming.BuildTypeofExpression(openDefinition),
            AssignableKeys = ImmutableArray<string>.Empty,
            ContractShapes = ContractReader.BuildContractShapes(closed),
            StagedConstructionExpression = stagedConstruction,
            StagedConstructionUsesKeyedServices = stagedKeyedServices,
            HasMultiplePublicConstructors = false,
            HasFromServicesConstructorParameter = false,
            InfoLocation = null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = null,
            HasIgnoredResultAdapter = false,
            MetadataSortKey = SymbolNaming.BuildMetadataName(closed),
        };
    }
}
