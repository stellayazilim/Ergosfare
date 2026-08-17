using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Closes open participants over the messages their constraints admit.
/// </summary>
/// <remarks>
/// A handler taking its message as a type parameter is one declaration and many pipelines.
/// Closing it here turns each of those into an ordinary registrable type, which is what lets
/// them be planned instead of resolved through an open generic at run time.
/// </remarks>
internal static class Monomorphizer
{
    /// <summary>
    /// Reports whether a generic level is still open.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns>
    /// <c>true</c> for the definition itself and for a form constructed from its own type
    /// parameters.
    /// </returns>
    /// <remarks>
    /// A form closed over concrete types is neither, and can be named.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a generic participant is one no message can bind.
    /// </summary>
    /// <param name="symbol">The participant to test.</param>
    /// <returns><c>true</c> when nothing binds it.</returns>
    /// <remarks>
    /// <para>
    /// A generic participant binds when its contract's message type is built from its own
    /// type parameters — <c>WrapHandler&lt;T&gt; : ICommandHandler&lt;Wrap&lt;T&gt;&gt;</c>.
    /// The table then keys the message by its definition and names the participant by its
    /// unbound <c>typeof</c>, and the dispatch closes the participant over the arguments of
    /// the message it carries. One baked entry serves every instantiation.
    /// </para>
    /// <para>
    /// It binds to nothing when the contract's message type is the type parameter itself —
    /// <c>ValidateCommands&lt;TCommand&gt; : ICommandPreInterceptor&lt;TCommand&gt;</c>. The
    /// message is then any concrete command, which carries no generic arguments to close the
    /// participant over, and participants are matched to messages by concrete type, so no
    /// message's stages ever hold it.
    /// </para>
    /// </remarks>
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
    /// Closes every unbindable open participant over the messages its constraint admits.
    /// </summary>
    /// <param name="compilation">The compilation to read participants and messages from.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>One closed model per participant-and-message pair.</returns>
    /// <remarks>
    /// <para>
    /// The set to close over does not come from the source — nobody writes
    /// <c>ValidateCommands&lt;RegisterUser&gt;</c>. It is the type parameter's constraint
    /// intersected with the compiled messages, and each surviving pair becomes a distinct
    /// type with its own registration and its own place in that message's pipeline.
    /// </para>
    /// <para>
    /// A participant that closes over nothing keeps ERGO016: it is registered and it still
    /// runs for no message.
    /// </para>
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

            // Only the single-parameter shape is closed here: the message is the one thing a
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
    /// Walks a namespace for the two halves this closing needs.
    /// </summary>
    /// <param name="ns">The namespace to walk.</param>
    /// <param name="openParticipants">
    /// The list open participants are added to; created on first use.
    /// </param>
    /// <param name="messages">The list candidate messages are added to; created on first use.</param>
    /// <param name="ct">Cancels the walk.</param>
    /// <remarks>
    /// Only the compilation's own types: a closed form is emitted into this assembly, so it
    /// is built from what this assembly declares.
    /// </remarks>
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

    /// <summary>
    /// Sorts one type, and everything nested in it, into open participants or messages.
    /// </summary>
    /// <param name="type">The type to sort.</param>
    /// <param name="openParticipants">
    /// The list open participants are added to; created on first use.
    /// </param>
    /// <param name="messages">The list candidate messages are added to; created on first use.</param>
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

        // A constraint admits messages, so what qualifies is a dispatchable type — which is
        // therefore not itself a participant.
        if (ContractReader.IsDispatchableMessage(type, ContractReader.BuildDescriptors(type)))
        {
            (messages ??= []).Add(type);
        }
    }

    /// <summary>
    /// Reports whether a message satisfies every constraint a type parameter declares.
    /// </summary>
    /// <param name="parameter">The participant's type parameter.</param>
    /// <param name="candidate">The message to close it over.</param>
    /// <returns><c>true</c> when every constraint holds.</returns>
    /// <remarks>
    /// The same question as whether <c>Participant&lt;Message&gt;</c> would have compiled had
    /// someone written it.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a candidate is the constraint type or derives from it.
    /// </summary>
    /// <param name="candidate">The message to test.</param>
    /// <param name="constraint">The constraint it must satisfy.</param>
    /// <returns><c>true</c> when the candidate is assignable to the constraint.</returns>
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
    /// Builds the model of one closed participant.
    /// </summary>
    /// <param name="closed">The closed form.</param>
    /// <param name="openDefinition">The declaration it was closed from.</param>
    /// <param name="currentAssembly">The compilation's assembly.</param>
    /// <returns>
    /// The model, or <c>null</c> when the closed form carries no contract to register.
    /// </returns>
    /// <remarks>
    /// The closed form is a type in its own right: it names itself in full rather than in the
    /// unbound form a declared generic uses, and its contracts name a concrete message, which
    /// is what lets the composition table bind it like any other participant. Its attributes
    /// are read from the declaration, which is where an author wrote them.
    /// </remarks>
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

        var typeofExpression = SymbolNaming.VerbatimTypeExpression(closed);
        var providerConstruction = ConstructionAnalyzer.GetProviderConstructionExpression(
            closed, typeofExpression, currentAssembly, out var usesKeyedServices);

        var stagedConstruction = ConstructionAnalyzer.TryBuildConstructionExpression(
            closed, typeofExpression, currentAssembly, "serviceProvider",
            allowParameterless: true, out var stagedKeyedServices);

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
            // It is bound now, so no longer the shape ERGO016 reports.
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
            ImplementsMessageMarker = true,
            DerivedEventMessages = ImmutableArray<RegistrableTypeModel>.Empty,
            MetadataSortKey = SymbolNaming.BuildMetadataName(closed),
        };
    }
}
