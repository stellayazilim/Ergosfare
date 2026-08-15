
using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of a registrable construct discovered in the compilation:
///     any user-declared type assignable to one of the module marker interfaces
///     (<c>ICommand</c>, <c>IQuery</c>, <c>IEvent</c>). Handlers and interceptors inherit
///     the marker through their contract interfaces, so a single marker check covers
///     messages, handlers and interceptors alike.
/// </summary>
/// <remarks>
///     Equality is implemented manually because <see cref="Descriptors"/> is a sequence;
///     the incremental pipeline relies on value equality for caching.
/// </remarks>
internal readonly struct RegistrableTypeModel : IEquatable<RegistrableTypeModel>
{
    /// <summary>
    ///     The <c>typeof</c> argument for the type, fully qualified with <c>global::</c>;
    ///     generic definitions use the unbound form (e.g. <c>global::App.Handler&lt;&gt;</c>).
    /// </summary>
    public required string TypeofExpression { get; init; }

    /// <summary>Human-readable type name used in diagnostics.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the type is assignable to the command module marker.</summary>
    public required bool IsCommand { get; init; }

    /// <summary>Whether the type is assignable to the query module marker.</summary>
    public required bool IsQuery { get; init; }

    /// <summary>Whether the type is assignable to the event module marker.</summary>
    public required bool IsEvent { get; init; }

    /// <summary>
    ///     Whether generated code (a sibling type in the same assembly) can reference the
    ///     type; private/protected nested and file-local types cannot be registered and are
    ///     reported via <c>ERGO001</c> instead.
    /// </summary>
    public required bool IsAccessible { get; init; }

    /// <summary>Declaration location, captured only for inaccessible types.</summary>
    public required LocationInfo? Location { get; init; }

    /// <summary>The <c>[Weight]</c> attribute value, or 0 when undeclared.</summary>
    public required uint Weight { get; init; }

    /// <summary>
    ///     The emitted C# expression for the <c>[Group]</c> names (e.g.
    ///     <c>new string[] { "a", "b" }</c>), or <c>null</c> when the type declares no
    ///     groups — the descriptor factory then applies the default group.
    /// </summary>
    public required string? GroupsExpression { get; init; }

    /// <summary>
    ///     The declared <c>[Group]</c> names behind <see cref="GroupsExpression"/>, empty
    ///     when the type declares none. Frozen compositions apply a message's
    ///     group-scoped <c>[ExcludeFromPipeline]</c> at emission, which needs the names
    ///     themselves rather than their emitted array expression.
    /// </summary>
    public required ImmutableArray<string> GroupNames { get; init; }

    /// <summary>
    ///     The pre-computed descriptors for the type's handler contracts. Empty for plain
    ///     messages and for open generic types (whose contract type arguments cannot be
    ///     expressed in <c>typeof</c>) — those fall back to runtime
    ///     <c>Register(Type)</c> registration.
    /// </summary>
    public required ImmutableArray<DescriptorModel> Descriptors { get; init; }

    /// <summary>
    ///     Name of the referenced assembly the type was discovered in, or <c>null</c> when
    ///     the type is declared in the current compilation. Selects between the
    ///     ERGO001 (source) and ERGO002 (reference) diagnostics for inaccessible types.
    /// </summary>
    public required string? ReferencedAssemblyName { get; init; }

    /// <summary>
    ///     The type's declared discovery keys (its own <c>[DiscoveryKey]</c>, else its
    ///     assembly's). Empty means the type participates in default discovery under the
    ///     implicit default key (the empty string).
    /// </summary>
    public required ImmutableArray<string> DiscoveryKeys { get; init; }

    /// <summary>
    ///     Whether the type can appear as a dispatched message instance — concrete
    ///     (non-abstract class or struct), fully closed, accessible, and carrying no
    ///     handler contracts — and therefore gets its dispatch generics rooted via
    ///     <c>GeneratedDispatchRoots</c>.
    /// </summary>
    public required bool IsDispatchableMessage { get; init; }

    /// <summary>
    ///     Whether the type is a message shape a frozen composition is computed for —
    ///     wider than <see cref="IsDispatchableMessage"/>: abstract bases and message
    ///     interfaces get entries too, because they are what the table's ancestor ladder
    ///     lands on for a message the generator never saw.
    /// </summary>
    public required bool IsMessageShape { get; init; }

    /// <summary>
    ///     The result roots to emit for a dispatchable message: one entry per closed
    ///     <c>ICommand&lt;T&gt;</c>/<c>IQuery&lt;T&gt;</c> (result) and
    ///     <c>IStreamQuery&lt;T&gt;</c> (stream) contract on the type. Empty for
    ///     non-dispatchable types.
    /// </summary>
    public required ImmutableArray<DispatchResultModel> DispatchResults { get; init; }

    /// <summary>
    ///     Whether generated code can construct the type with <c>new()</c> and doing so is
    ///     interchangeable with a plain transient container resolution: a concrete,
    ///     non-generic class with an accessible parameterless constructor that is neither
    ///     <c>IDisposable</c> nor <c>IAsyncDisposable</c>. Feeds the pipeline plans'
    ///     direct-construction factories; meaningful for handler types only.
    /// </summary>
    public required bool IsDirectlyConstructible { get; init; }

    /// <summary>
    ///     The emitted provider-taking construction factory
    ///     (<c>static provider =&gt; new THandler(provider.GetRequiredService&lt;TDep&gt;(), ...)</c>)
    ///     for a handler whose single public constructor takes only plain (or
    ///     <c>[FromKeyedServices]</c>) service parameters, or <c>null</c> when the type
    ///     does not qualify. Mutually exclusive with
    ///     <see cref="IsDirectlyConstructible"/> — parameterless construction stays on the
    ///     cheaper <c>Func&lt;THandler&gt;</c> shape. Meaningful for handler types only.
    /// </summary>
    public required string? ProviderConstructionExpression { get; init; }

    /// <summary>
    ///     Whether <see cref="ProviderConstructionExpression"/> resolves any parameter
    ///     through the keyed-service extensions; emission then additionally requires
    ///     those extensions to be resolvable in the consuming compilation.
    /// </summary>
    public required bool ProviderConstructionUsesKeyedServices { get; init; }

    /// <summary>Whether the type carries <c>[ExcludeFromPipeline]</c>; such messages stay off staged plans.</summary>
    public required bool HasPipelineExclusion { get; init; }

    /// <summary>
    ///     The interceptor group names the type's <c>[ExcludeFromPipeline]</c> names, or
    ///     empty when the attribute is absent or parameterless. Empty with
    ///     <see cref="HasPipelineExclusion"/> set is the blanket exclusion: every
    ///     covariantly matched interceptor drops out. Frozen compositions bake the
    ///     resulting stages, which is why the group list — not just the attribute's
    ///     presence — has to travel in the model.
    /// </summary>
    public required ImmutableArray<string> ExcludedInterceptorGroups { get; init; }

    /// <summary>Whether the type is a value type — variance never applies to it at runtime.</summary>
    public required bool IsValueType { get; init; }

    /// <summary>
    ///     Whether the type is nested in another type. The runtime orders pipeline
    ///     segments by <c>Type.FullName</c>, whose nested separator (<c>+</c>) sorts
    ///     differently from the display name's dot — nested participants therefore
    ///     disqualify staged plans instead of risking a divergent order.
    /// </summary>
    public required bool IsNestedType { get; init; }

    /// <summary>
    ///     Whether this is a generic participant no message can bind: one whose contract
    ///     names a bare type parameter as its message. It registers like any other
    ///     participant and then appears in no pipeline — participants are matched to
    ///     messages by concrete type, and a concrete message carries no generic arguments
    ///     to close this one over. It never executes, and until ERGO016 it never said so.
    /// </summary>
    /// <remarks>
    ///     Not every generic participant: one whose contract's message type is built from
    ///     its own type parameters (a handler for a generic message) binds fine — the table
    ///     keys the message by its definition and the dispatch closes the participant over
    ///     the message's arguments.
    /// </remarks>
    public required bool IsGenericParticipant { get; init; }

    /// <summary>
    ///     For a monomorphized participant: the <c>typeof</c> expression of the open
    ///     definition it was closed from. <c>null</c> for every declared type.
    /// </summary>
    /// <remarks>
    ///     The link back matters for exactly one thing: a definition that closed over at
    ///     least one message is not the shape ERGO016 reports, and the only way to know is
    ///     to see whether anything came out of it.
    /// </remarks>
    public string? MonomorphizedFrom { get; init; }

    /// <summary>
    ///     For dispatchable messages: the normalized type expressions of every base type
    ///     and implemented interface — the compile-time domain of the runtime's
    ///     <c>IsAssignableTo</c> checks that admit indirect (covariant) interceptors.
    ///     Empty for non-dispatchable types.
    /// </summary>
    public required ImmutableArray<string> AssignableKeys { get; init; }

    /// <summary>
    ///     The raw interceptor contracts the type implements (undeduped), feeding the
    ///     staged-plan arm selection; see <see cref="ContractShapeModel"/>. Empty for
    ///     plain messages.
    /// </summary>
    public required ImmutableArray<ContractShapeModel> ContractShapes { get; init; }

    /// <summary>
    ///     The bare <c>new T(...)</c> construction expression for a pipeline participant
    ///     whose construction is provably identical to container activation, resolving
    ///     constructor dependencies from the <c>serviceProvider</c> identifier — the
    ///     staged plans' direct-construction (<c>ExecuteDirect</c>) emission input.
    ///     <c>null</c> when the participant does not qualify.
    /// </summary>
    public required string? StagedConstructionExpression { get; init; }

    /// <summary>
    ///     Whether <see cref="StagedConstructionExpression"/> resolves any dependency
    ///     through the keyed-service extensions.
    /// </summary>
    public required bool StagedConstructionUsesKeyedServices { get; init; }

    /// <summary>
    ///     Whether a pipeline participant declares more than one public constructor —
    ///     the ERGO003 info: the container's constructor selection stays in play, so
    ///     generated plans skip the direct-construction fast path.
    /// </summary>
    public required bool HasMultiplePublicConstructors { get; init; }

    /// <summary>
    ///     Whether any constructor parameter carries <c>[FromServices]</c> — the
    ///     ERGO004 info: the attribute has no effect on constructors.
    /// </summary>
    public required bool HasFromServicesConstructorParameter { get; init; }

    /// <summary>
    ///     Declaration location for the informational diagnostics above and the
    ///     unreachable-handler diagnostics (ERGO007/008); captured for source-declared
    ///     types when one of the constructor infos applies or the type carries handler
    ///     contracts.
    /// </summary>
    public required LocationInfo? InfoLocation { get; init; }

    /// <summary>
    ///     Whether the type opted out of discovery via <c>[ExcludeFromDiscovery]</c> (its
    ///     own or its assembly's). Excluded types produce no registration and no emission
    ///     — they exist in the model only as the reachability judgment's exclusion zone:
    ///     their runtime participation is manual and unknowable, so every verdict touching
    ///     them abstains. Shadow models carry only the fields the zone needs
    ///     (assignable keys, main-handler descriptor messages).
    /// </summary>
    public required bool IsExcludedFromDiscovery { get; init; }

    /// <summary>
    ///     The type's CLR <c>FullName</c>-shaped identity (namespace-qualified, nested
    ///     with <c>+</c>, generic arity with backticks) — the string the runtime stage
    ///     comparator orders by, captured so frozen compositions can bake the exact
    ///     runtime order for nested and generic participants too.
    /// </summary>
    public required string MetadataSortKey { get; init; }

    /// <summary>
    ///     The message's <c>[ResultAdapter]</c> annotation (its own or an inherited one),
    ///     projected for the staged plans' baked binding and the ERGO011 judgment.
    ///     <c>null</c> for unannotated types and for non-dispatchable ones — captured only
    ///     where the runtime binding would consult it.
    /// </summary>
    public required ResultAdapterModel? ResultAdapter { get; init; }

    /// <summary>
    ///     Whether the message carries <c>[IgnoreResultAdapter]</c> (its own or an
    ///     inherited one): every adapter tier — annotation, native, configured default —
    ///     is suppressed and the pipelines keep the classic try/catch semantics.
    ///     Combined with an effective <see cref="ResultAdapter"/> annotation, the
    ///     contradiction is ERGO012.
    /// </summary>
    public required bool HasIgnoredResultAdapter { get; init; }

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
            || !Nullable.Equals(InfoLocation, other.InfoLocation)
            || Descriptors.Length != other.Descriptors.Length
            || DiscoveryKeys.Length != other.DiscoveryKeys.Length
            || ExcludedInterceptorGroups.Length != other.ExcludedInterceptorGroups.Length
            || GroupNames.Length != other.GroupNames.Length
            || DispatchResults.Length != other.DispatchResults.Length
            || AssignableKeys.Length != other.AssignableKeys.Length
            || ContractShapes.Length != other.ContractShapes.Length)
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

        return true;
    }

    public override bool Equals(object? obj) => obj is RegistrableTypeModel other && Equals(other);

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
