using System;
using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of a registrable construct discovered in the compilation:
///     any user-declared type assignable to one of the module marker interfaces
///     (<c>ICommand</c>, <c>IQuery</c>, <c>IEvent</c>). Handlers and interceptors inherit
///     the marker through their contract interfaces, so a single marker check covers
///     messages, handlers and interceptors alike — mirroring what the reflection-based
///     <c>RegisterFromAssembly</c> discovers at runtime.
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
    ///     reported via <c>ERGOSG001</c> instead.
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
    ///     The pre-computed descriptors for the type's handler contracts. Empty for plain
    ///     messages and for open generic types (whose contract type arguments cannot be
    ///     expressed in <c>typeof</c>) — those fall back to runtime
    ///     <c>Register(Type)</c> registration.
    /// </summary>
    public required ImmutableArray<DescriptorModel> Descriptors { get; init; }

    /// <summary>
    ///     Name of the referenced assembly the type was discovered in, or <c>null</c> when
    ///     the type is declared in the current compilation. Selects between the
    ///     ERGOSG001 (source) and ERGOSG002 (reference) diagnostics for inaccessible types.
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
    ///     the ERGOSG003 info: the container's constructor selection stays in play, so
    ///     generated plans skip the direct-construction fast path.
    /// </summary>
    public required bool HasMultiplePublicConstructors { get; init; }

    /// <summary>
    ///     Whether any constructor parameter carries <c>[FromServices]</c> — the
    ///     ERGOSG004 info: the attribute has no effect on constructors.
    /// </summary>
    public required bool HasFromServicesConstructorParameter { get; init; }

    /// <summary>
    ///     Declaration location for the informational diagnostics above; captured only
    ///     when one of them applies (source-declared types only).
    /// </summary>
    public required LocationInfo? InfoLocation { get; init; }

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
            || IsDirectlyConstructible != other.IsDirectlyConstructible
            || ProviderConstructionExpression != other.ProviderConstructionExpression
            || ProviderConstructionUsesKeyedServices != other.ProviderConstructionUsesKeyedServices
            || HasPipelineExclusion != other.HasPipelineExclusion
            || IsValueType != other.IsValueType
            || IsNestedType != other.IsNestedType
            || StagedConstructionExpression != other.StagedConstructionExpression
            || StagedConstructionUsesKeyedServices != other.StagedConstructionUsesKeyedServices
            || HasMultiplePublicConstructors != other.HasMultiplePublicConstructors
            || HasFromServicesConstructorParameter != other.HasFromServicesConstructorParameter
            || !Nullable.Equals(InfoLocation, other.InfoLocation)
            || Descriptors.Length != other.Descriptors.Length
            || DiscoveryKeys.Length != other.DiscoveryKeys.Length
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
