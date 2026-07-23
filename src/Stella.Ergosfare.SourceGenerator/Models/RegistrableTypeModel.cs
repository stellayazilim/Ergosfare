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
            || Descriptors.Length != other.Descriptors.Length
            || DiscoveryKeys.Length != other.DiscoveryKeys.Length
            || DispatchResults.Length != other.DispatchResults.Length)
        {
            return false;
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
