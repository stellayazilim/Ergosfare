
using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One registrable construct, as a value the incremental pipeline can compare.
/// </summary>
/// <remarks>
/// A construct is any user-declared type reaching a module marker — <c>ICommand</c>,
/// <c>IQuery</c>, <c>IEvent</c>. A message declares one itself; a handler or interceptor
/// inherits one through its contract, so a single marker check covers all three. Equality is
/// written by hand because several members are sequences, and the pipeline's caching turns on
/// it.
/// </remarks>
internal readonly struct RegistrableTypeModel : IEquatable<RegistrableTypeModel>
{
    /// <summary>
    /// The <c>typeof</c> argument naming this type, qualified with <c>global::</c>.
    /// </summary>
    /// <remarks>
    /// A generic definition is named unbound, as in <c>global::App.Handler&lt;&gt;</c>.
    /// </remarks>
    public required string TypeofExpression { get; init; }

    /// <summary>The readable type name diagnostics use.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the type reaches the command marker.</summary>
    public required bool IsCommand { get; init; }

    /// <summary>Whether the type reaches the query marker.</summary>
    public required bool IsQuery { get; init; }

    /// <summary>Whether the type reaches the event marker.</summary>
    public required bool IsEvent { get; init; }

    /// <summary>
    /// Whether generated code — a sibling type in the same assembly — can name this type.
    /// </summary>
    /// <remarks>
    /// A private or protected nested type and a file-local one cannot be registered, and are
    /// reported as ERGO001 instead.
    /// </remarks>
    public required bool IsAccessible { get; init; }

    /// <summary>The declaration's location, captured only for an inaccessible type.</summary>
    public required LocationInfo? Location { get; init; }

    /// <summary>The <c>[Weight]</c> value, or <c>0</c> when the type declares none.</summary>
    public required uint Weight { get; init; }

    /// <summary>
    /// The array expression for the <c>[Group]</c> names, such as
    /// <c>new string[] { "a", "b" }</c>, or <c>null</c> when the type declares no groups —
    /// the descriptor then applies the default group.
    /// </summary>
    public required string? GroupsExpression { get; init; }

    /// <summary>
    /// The declared <c>[Group]</c> names behind <see cref="GroupsExpression"/>, empty when
    /// the type declares none.
    /// </summary>
    /// <remarks>
    /// A frozen composition resolves a message's group-scoped <c>[ExcludeFromPipeline]</c>
    /// while it is built, which needs the names themselves rather than the array expression.
    /// </remarks>
    public required ImmutableArray<string> GroupNames { get; init; }

    /// <summary>
    /// The descriptors for the type's handler contracts; empty for a plain message.
    /// </summary>
    /// <remarks>
    /// Also empty for an open generic type, whose contract arguments cannot be written as a
    /// <c>typeof</c>; such a type falls back to <c>Register(Type)</c> at run time.
    /// </remarks>
    public required ImmutableArray<DescriptorModel> Descriptors { get; init; }

    /// <summary>
    /// The name of the referenced assembly the type was found in, or <c>null</c> when this
    /// compilation declares it.
    /// </summary>
    /// <remarks>
    /// Chooses between ERGO001 and ERGO002 for an inaccessible type.
    /// </remarks>
    public required string? ReferencedAssemblyName { get; init; }

    /// <summary>
    /// The type's discovery keys: its own <c>[DiscoveryKey]</c> when it declares any,
    /// otherwise its assembly's.
    /// </summary>
    /// <remarks>
    /// Empty means the type takes part in default discovery, under the implicit empty key.
    /// </remarks>
    public required ImmutableArray<string> DiscoveryKeys { get; init; }

    /// <summary>
    /// Whether the type can appear as a dispatched message instance, and so has its dispatch
    /// generics rooted.
    /// </summary>
    /// <remarks>
    /// It has to be concrete, fully closed, accessible, and carry no handler contract.
    /// </remarks>
    public required bool IsDispatchableMessage { get; init; }

    /// <summary>
    /// Whether a frozen composition is computed for this type.
    /// </summary>
    /// <remarks>
    /// Wider than <see cref="IsDispatchableMessage"/>: an abstract base and a message
    /// interface get entries too, because they are where the table's ancestor ladder lands
    /// for a message this compilation never saw.
    /// </remarks>
    public required bool IsMessageShape { get; init; }

    /// <summary>
    /// The result roots to emit for a dispatchable message: one entry per closed
    /// <c>ICommand&lt;T&gt;</c> or <c>IQuery&lt;T&gt;</c> result contract and per
    /// <c>IStreamQuery&lt;T&gt;</c> stream contract. Empty for anything else.
    /// </summary>
    public required ImmutableArray<DispatchResultModel> DispatchResults { get; init; }

    /// <summary>
    /// Whether generated code can build this type with <c>new()</c> and doing so is
    /// interchangeable with resolving its plain transient registration.
    /// </summary>
    /// <remarks>
    /// It has to be a concrete, non-generic class whose only instance constructor is public
    /// and parameterless, implementing neither <c>IDisposable</c> nor
    /// <c>IAsyncDisposable</c>. Feeds the plans' direct-construction factories, so it is
    /// meaningful for handler types only.
    /// </remarks>
    public required bool IsDirectlyConstructible { get; init; }

    /// <summary>
    /// The provider-taking construction factory for a handler whose single public constructor
    /// takes only service parameters, plain or <c>[FromKeyedServices]</c>; <c>null</c> when
    /// the handler does not qualify.
    /// </summary>
    /// <remarks>
    /// Never set together with <see cref="IsDirectlyConstructible"/>: a parameterless
    /// construction stays on the cheaper <c>Func&lt;THandler&gt;</c> shape. Meaningful for
    /// handler types only.
    /// </remarks>
    public required string? ProviderConstructionExpression { get; init; }

    /// <summary>
    /// Whether <see cref="ProviderConstructionExpression"/> resolves any parameter through
    /// the keyed-service extensions, which emission then requires the consuming compilation
    /// to be able to resolve.
    /// </summary>
    public required bool ProviderConstructionUsesKeyedServices { get; init; }

    /// <summary>
    /// Whether the type carries <c>[ExcludeFromPipeline]</c>; such a message gets no staged
    /// plan.
    /// </summary>
    public required bool HasPipelineExclusion { get; init; }

    /// <summary>
    /// The interceptor groups the type's <c>[ExcludeFromPipeline]</c> names, empty when the
    /// attribute is absent or parameterless.
    /// </summary>
    /// <remarks>
    /// Empty with <see cref="HasPipelineExclusion"/> set is the blanket form: every
    /// covariantly matched interceptor drops out. The frozen composition bakes the resulting
    /// stages, which is why the names travel in the model and not just the attribute's
    /// presence.
    /// </remarks>
    public required ImmutableArray<string> ExcludedInterceptorGroups { get; init; }

    /// <summary>
    /// Whether the type is a value type, which variance never applies to.
    /// </summary>
    public required bool IsValueType { get; init; }

    /// <summary>
    /// Whether the type is nested in another type.
    /// </summary>
    /// <remarks>
    /// The runtime orders pipeline segments by <c>Type.FullName</c>, whose nested separator
    /// sorts differently from the display name's dot, so a nested participant disqualifies a
    /// staged plan rather than risk a divergent order.
    /// </remarks>
    public required bool IsNestedType { get; init; }

    /// <summary>
    /// Whether this is a generic participant no message can bind — one whose contract names a
    /// bare type parameter as its message.
    /// </summary>
    /// <remarks>
    /// It registers like any other participant and then appears in no pipeline: participants
    /// are matched by concrete type, and a concrete message carries no generic arguments to
    /// close it over. Not every generic participant is this shape — one whose contract builds
    /// its message from its own type parameters, a handler for a generic message, binds fine.
    /// Reported as ERGO016.
    /// </remarks>
    public required bool IsGenericParticipant { get; init; }

    /// <summary>
    /// For a closed participant, the <c>typeof</c> expression of the open definition it came
    /// from; <c>null</c> for every declared type.
    /// </summary>
    /// <remarks>
    /// The link back settles one question: a definition that closed over at least one message
    /// is not the shape ERGO016 reports, and the only way to know is to see whether anything
    /// came out of it.
    /// </remarks>
    public string? MonomorphizedFrom { get; init; }

    /// <summary>
    /// For a message, the normalized expressions of every base type and implemented
    /// interface; empty for anything else.
    /// </summary>
    /// <remarks>
    /// The compile-time domain of the runtime's assignability check, which is what admits
    /// covariantly registered interceptors.
    /// </remarks>
    public required ImmutableArray<string> AssignableKeys { get; init; }

    /// <summary>
    /// The interceptor contracts the type implements, undeduped; empty for a plain message.
    /// </summary>
    /// <remarks>
    /// What the staged plans select a call's contract from; see
    /// <see cref="ContractShapeModel"/>.
    /// </remarks>
    public required ImmutableArray<ContractShapeModel> ContractShapes { get; init; }

    /// <summary>
    /// The bare <c>new T(...)</c> expression for a participant whose construction is
    /// indistinguishable from container activation, resolving its dependencies from the
    /// <c>serviceProvider</c> identifier; <c>null</c> when it does not qualify.
    /// </summary>
    /// <remarks>
    /// What the staged plans emit for a directly constructed call.
    /// </remarks>
    public required string? StagedConstructionExpression { get; init; }

    /// <summary>
    /// Whether <see cref="StagedConstructionExpression"/> resolves any dependency through the
    /// keyed-service extensions.
    /// </summary>
    public required bool StagedConstructionUsesKeyedServices { get; init; }

    /// <summary>
    /// Whether a participant declares more than one public constructor, which keeps it on the
    /// container path. Reported as ERGO003.
    /// </summary>
    public required bool HasMultiplePublicConstructors { get; init; }

    /// <summary>
    /// Whether any constructor parameter carries <c>[FromServices]</c>, where it does
    /// nothing. Reported as ERGO004.
    /// </summary>
    public required bool HasFromServicesConstructorParameter { get; init; }

    /// <summary>
    /// The declaration's location for the informational diagnostics and for ERGO007 and
    /// ERGO008.
    /// </summary>
    /// <remarks>
    /// Captured for a source-declared type when one of those findings could apply to it.
    /// </remarks>
    public required LocationInfo? InfoLocation { get; init; }

    /// <summary>
    /// Whether the type opted out of discovery, through its own <c>[ExcludeFromDiscovery]</c>
    /// or its assembly's.
    /// </summary>
    /// <remarks>
    /// Such a type is never registered. It stays in the model as the reachability judgment's
    /// exclusion zone — how it takes part at run time is decided by hand and cannot be known
    /// here, so every verdict touching it abstains — and carries only the fields that zone
    /// and emission need.
    /// </remarks>
    public required bool IsExcludedFromDiscovery { get; init; }

    /// <summary>
    /// The type's CLR <c>FullName</c>-shaped identity: namespace-qualified, nested with
    /// <c>+</c>, generic arity with backticks.
    /// </summary>
    /// <remarks>
    /// The string the runtime's stage comparator orders by, captured so a frozen composition
    /// can bake that exact order for nested and generic participants too.
    /// </remarks>
    public required string MetadataSortKey { get; init; }

    /// <summary>
    /// The message's <c>[ResultAdapter]</c> annotation, its own or an inherited one;
    /// <c>null</c> when it declares none or is not dispatchable.
    /// </summary>
    /// <remarks>
    /// Captured only where the runtime binding would consult it, and read by the staged
    /// plans' baked binding and by ERGO011.
    /// </remarks>
    public required ResultAdapterModel? ResultAdapter { get; init; }

    /// <summary>
    /// Whether the message carries <c>[IgnoreResultAdapter]</c>, its own or an inherited one.
    /// </summary>
    /// <remarks>
    /// Every adapter tier is then suppressed — annotation, native and configured default —
    /// and the pipeline keeps its try/catch semantics. Together with an effective
    /// <see cref="ResultAdapter"/>, the contradiction is ERGO012.
    /// </remarks>
    public required bool HasIgnoredResultAdapter { get; init; }

    /// <summary>
    /// Whether the type implements <c>IMessage</c> through a module marker of its own.
    /// </summary>
    /// <remarks>
    /// False only for an event message derived from a subscriber's signature: the publish
    /// lane asks for <c>notnull</c>, so a plain domain type travels it without ever
    /// implementing the marker. Emission reads this to decide whether the type may be named
    /// where <c>IMessage</c> is required — <c>AddMessage&lt;T&gt;</c> and the message-root
    /// table behind it. A broadcast never needs that root: the publish surface is generic over
    /// the event, so its dispatch closes inside a generic context, and the runtime type path
    /// that consults the root cannot be reached by a type with no base contract to be
    /// dispatched through.
    /// </remarks>
    public required bool ImplementsMessageMarker { get; init; }

    /// <summary>
    /// The event messages this type's handler contracts name, for the ones carrying no module
    /// marker of their own.
    /// </summary>
    /// <remarks>
    /// A plain domain type is not a message until an <c>IEventHandler&lt;T&gt;</c> is written
    /// for it; that signature is not evidence pointing at a message, it is what makes one, so
    /// the model is born here rather than from a declaration nothing would visit.
    /// <para>
    /// They travel inside this model instead of widening the syntax provider to an array of
    /// models. Roslyn compares a provider's output with
    /// <c>EqualityComparer&lt;T&gt;.Default</c> to decide whether the rest of the pipeline
    /// can be skipped, and <c>ImmutableArray&lt;T&gt;</c> compares by the underlying array's
    /// reference — a fresh array every run, so the comparison would never hold and every
    /// keystroke would silently rerun planning and emission. Nested here, the compared value
    /// stays this struct, whose equality reads arrays element by element.
    /// </para>
    /// </remarks>
    public required ImmutableArray<RegistrableTypeModel> DerivedEventMessages { get; init; }

    /// <summary>
    /// Compares this model with another, member by member and element by element.
    /// </summary>
    /// <param name="other">The model to compare with.</param>
    /// <returns><c>true</c> when both describe the same construct identically.</returns>
    public bool Equals(RegistrableTypeModel other)
    {
        if (TypeofExpression != other.TypeofExpression
            || DisplayName != other.DisplayName
            || IsCommand != other.IsCommand
            || IsQuery != other.IsQuery
            || IsEvent != other.IsEvent
            || IsAccessible != other.IsAccessible
            || !Nullable.Equals(Location, other.Location)
            || Weight != other.Weight
            || GroupsExpression != other.GroupsExpression
            || ReferencedAssemblyName != other.ReferencedAssemblyName
            || IsDispatchableMessage != other.IsDispatchableMessage
            || IsMessageShape != other.IsMessageShape
            || IsDirectlyConstructible != other.IsDirectlyConstructible
            || ProviderConstructionExpression != other.ProviderConstructionExpression
            || ProviderConstructionUsesKeyedServices != other.ProviderConstructionUsesKeyedServices
            || HasPipelineExclusion != other.HasPipelineExclusion
            || IsValueType != other.IsValueType
            || IsNestedType != other.IsNestedType
            || IsGenericParticipant != other.IsGenericParticipant
            || MonomorphizedFrom != other.MonomorphizedFrom
            || StagedConstructionExpression != other.StagedConstructionExpression
            || StagedConstructionUsesKeyedServices != other.StagedConstructionUsesKeyedServices
            || HasMultiplePublicConstructors != other.HasMultiplePublicConstructors
            || HasFromServicesConstructorParameter != other.HasFromServicesConstructorParameter
            || IsExcludedFromDiscovery != other.IsExcludedFromDiscovery
            || MetadataSortKey != other.MetadataSortKey
            || !Equals(ResultAdapter, other.ResultAdapter)
            || HasIgnoredResultAdapter != other.HasIgnoredResultAdapter
            || ImplementsMessageMarker != other.ImplementsMessageMarker
            || !Nullable.Equals(InfoLocation, other.InfoLocation)
            || Descriptors.Length != other.Descriptors.Length
            || DiscoveryKeys.Length != other.DiscoveryKeys.Length
            || ExcludedInterceptorGroups.Length != other.ExcludedInterceptorGroups.Length
            || GroupNames.Length != other.GroupNames.Length
            || DispatchResults.Length != other.DispatchResults.Length
            || AssignableKeys.Length != other.AssignableKeys.Length
            || ContractShapes.Length != other.ContractShapes.Length
            || DerivedEventMessages.Length != other.DerivedEventMessages.Length)
        {
            return false;
        }

        for (var i = 0; i < AssignableKeys.Length; i++)
        {
            if (!string.Equals(AssignableKeys[i], other.AssignableKeys[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (var i = 0; i < ContractShapes.Length; i++)
        {
            if (!ContractShapes[i].Equals(other.ContractShapes[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < Descriptors.Length; i++)
        {
            if (!Descriptors[i].Equals(other.Descriptors[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < DiscoveryKeys.Length; i++)
        {
            if (!string.Equals(DiscoveryKeys[i], other.DiscoveryKeys[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (var i = 0; i < ExcludedInterceptorGroups.Length; i++)
        {
            if (!string.Equals(ExcludedInterceptorGroups[i], other.ExcludedInterceptorGroups[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (var i = 0; i < GroupNames.Length; i++)
        {
            if (!string.Equals(GroupNames[i], other.GroupNames[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (var i = 0; i < DispatchResults.Length; i++)
        {
            if (!DispatchResults[i].Equals(other.DispatchResults[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < DerivedEventMessages.Length; i++)
        {
            if (!DerivedEventMessages[i].Equals(other.DerivedEventMessages[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares this model with an object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> when it is an equal model.</returns>
    public override bool Equals(object? obj) => obj is RegistrableTypeModel other && Equals(other);

    /// <summary>
    /// Gives a hash over the few members that already separate most models.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = TypeofExpression.GetHashCode();
            hash = (hash * 397) ^ Weight.GetHashCode();
            hash = (hash * 397) ^ Descriptors.Length;
            hash = (hash * 397) ^ (GroupsExpression?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
