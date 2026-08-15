using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of one dispatch site: a mediator dispatch invocation
///     (<c>SendAsync</c>, <c>QueryAsync</c>, <c>StreamAsync</c>, <c>PublishAsync</c>,
///     <c>DispatchAsync</c>, <c>Mediate</c>) together with the static type of its message
///     argument, casts and conversions looked through. The static type is the compile-time
///     evidence the reachability judgment works from: its assignable keys mirror the
///     runtime's actual-or-first-assignable resolution, and its subtypes in the compiled
///     closure bound what a runtime instance could actually be.
/// </summary>
internal readonly struct DispatchSiteModel : IEquatable<DispatchSiteModel>
{
    /// <summary>
    ///     Normalized type expression of the static message type (generics reduced to
    ///     their unbound definitions), comparable against the registrable models'
    ///     <c>TypeofExpression</c>/<c>AssignableKeys</c> strings.
    /// </summary>
    public required string MessageTypeExpression { get; init; }

    /// <summary>
    ///     CLR metadata name of the static message type — the manifest attribute's
    ///     payload, resolvable back to a symbol by an aggregating compilation.
    /// </summary>
    public required string MessageTypeMetadataName { get; init; }

    /// <summary>Human-readable type name used in diagnostics.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dispatch surface the site goes through.</summary>
    public required DispatchSiteKind Kind { get; init; }

    /// <summary>
    ///     Whether the static type proves nothing about the concrete message: the bare
    ///     module marker, <c>IMessage</c>, <c>object</c>, or an unconstrained type
    ///     parameter. Opaque sites still contribute to reachability conservatively;
    ///     opacity only drives the strict-mode ERGO009 diagnostic.
    /// </summary>
    public required bool IsOpaque { get; init; }

    /// <summary>
    ///     Whether the static type is a value type — runtime contract variance never
    ///     applies to it, so coverage through assignable keys is off for the site itself.
    /// </summary>
    public required bool IsValueType { get; init; }

    /// <summary>
    ///     Whether the static type is (or constructs) a generic type. Generic dispatch
    ///     descriptor matching is definition-fuzzy, so such sites contribute to
    ///     reachability but never receive dead-dispatch verdicts.
    /// </summary>
    public required bool IsGenericMessage { get; init; }

    /// <summary>
    ///     Normalized type expressions of the static type's base types and interfaces —
    ///     the compile-time domain of the runtime's assignable-handler resolution.
    /// </summary>
    public required ImmutableArray<string> AssignableKeys { get; init; }

    /// <summary>
    ///     The group names this site dispatches under, normalized (ordinal-sorted,
    ///     deduplicated) so two spellings of one set compare equal. Empty means the site
    ///     names no group and runs the default one — which is also what
    ///     <see cref="HasUnprovableGroups"/> sites fall back to for keying purposes.
    /// </summary>
    public required ImmutableArray<string> Groups { get; init; }

    /// <summary>
    ///     Whether the site passes a group filter the generator could not read — a variable,
    ///     a computed set, a non-literal element. Such a site keys no plan; its message keeps
    ///     the runtime group lane, which filters the live composition per dispatch.
    /// </summary>
    public required bool HasUnprovableGroups { get; init; }

    /// <summary>Invocation location; <c>null</c> for sites rehydrated from a referenced manifest.</summary>
    public required LocationInfo? Location { get; init; }

    /// <summary>
    ///     Name of the referenced assembly whose manifest carried the site, or <c>null</c>
    ///     when the site is an invocation in the current compilation.
    /// </summary>
    public required string? ReferencedAssemblyName { get; init; }

    public bool Equals(DispatchSiteModel other)
    {
        if (MessageTypeExpression != other.MessageTypeExpression
            || MessageTypeMetadataName != other.MessageTypeMetadataName
            || DisplayName != other.DisplayName
            || Kind != other.Kind
            || IsOpaque != other.IsOpaque
            || IsValueType != other.IsValueType
            || IsGenericMessage != other.IsGenericMessage
            || HasUnprovableGroups != other.HasUnprovableGroups
            || ReferencedAssemblyName != other.ReferencedAssemblyName
            || !Nullable.Equals(Location, other.Location)
            || AssignableKeys.Length != other.AssignableKeys.Length
            || Groups.Length != other.Groups.Length)
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

        for (var i = 0; i < Groups.Length; i++)
        {
            if (!string.Equals(Groups[i], other.Groups[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is DispatchSiteModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = MessageTypeExpression.GetHashCode();
            hash = (hash * 397) ^ (int)Kind;
            hash = (hash * 397) ^ AssignableKeys.Length;
            hash = (hash * 397) ^ Groups.Length;
            return hash;
        }
    }
}
