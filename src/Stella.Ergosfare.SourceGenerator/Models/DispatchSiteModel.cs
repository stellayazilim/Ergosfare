using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One dispatch site: a call to a mediator, together with the static type of the message it
/// was given.
/// </summary>
/// <remarks>
/// Casts and conversions are looked through to find that static type, and it is the only
/// evidence reachability judgment has: the types it is assignable to mirror how the runtime
/// picks a handler, and its subtypes in the compiled program bound what the message could
/// actually be.
/// </remarks>
internal readonly struct DispatchSiteModel : IEquatable<DispatchSiteModel>
{
    /// <summary>
    /// The static message type, normalized so a constructed generic and its definition
    /// compare equal — which is what makes it comparable against a registered type's own
    /// expressions.
    /// </summary>
    public required string MessageTypeExpression { get; init; }

    /// <summary>
    /// The static message type's CLR metadata name, which is what a manifest records and
    /// what another compilation resolves back to a symbol.
    /// </summary>
    public required string MessageTypeMetadataName { get; init; }

    /// <summary>
    /// The message type as a diagnostic would name it.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Which mediator surface the site calls.
    /// </summary>
    public required DispatchSiteKind Kind { get; init; }

    /// <summary>
    /// Whether the static type proves nothing about the concrete message — a bare module
    /// marker, <c>IMessage</c>, <c>object</c>, or an unconstrained type parameter.
    /// </summary>
    /// <remarks>
    /// Such a site still counts towards reachability, conservatively; being opaque only
    /// matters to the strict-mode ERGO009 diagnostic.
    /// </remarks>
    public required bool IsOpaque { get; init; }

    /// <summary>
    /// Whether the static type is a value type, which cannot be reached through a base type
    /// — so coverage through assignable types does not apply to this site.
    /// </summary>
    public required bool IsValueType { get; init; }

    /// <summary>
    /// Whether the static type is generic or constructs a generic.
    /// </summary>
    /// <remarks>
    /// Handlers for a generic message are matched by definition rather than exactly, so such
    /// a site counts towards reachability but is never reported as a dead dispatch.
    /// </remarks>
    public required bool IsGenericMessage { get; init; }

    /// <summary>
    /// The static type's base types and interfaces, normalized — the types a handler could
    /// be registered against and still serve this message.
    /// </summary>
    public required ImmutableArray<string> AssignableKeys { get; init; }

    /// <summary>
    /// The groups this site dispatches under, sorted and deduplicated so two spellings of
    /// one set compare equal. Empty means the site names none and runs the default group.
    /// </summary>
    public required ImmutableArray<string> Groups { get; init; }

    /// <summary>
    /// Whether the site's groups could not be read — a variable, a computed set, or a
    /// non-literal element.
    /// </summary>
    /// <remarks>
    /// Such a site keys no plan; its message keeps the runtime group path, which filters the
    /// live composition on each dispatch.
    /// </remarks>
    public required bool HasUnprovableGroups { get; init; }

    /// <summary>
    /// Where the call is, or <c>null</c> for a site read back from a referenced assembly's
    /// manifest.
    /// </summary>
    public required LocationInfo? Location { get; init; }

    /// <summary>
    /// The referenced assembly whose manifest recorded this site, or <c>null</c> when the
    /// call is in the current compilation.
    /// </summary>
    public required string? ReferencedAssemblyName { get; init; }

    /// <summary>
    /// Compares every recorded fact about the site.
    /// </summary>
    /// <param name="other">The site to compare against.</param>
    /// <returns><c>true</c> when both describe the same dispatch.</returns>
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

    /// <summary>
    /// Compares against another dispatch site.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is an equal site.</returns>
    public override bool Equals(object? obj) => obj is DispatchSiteModel other && Equals(other);

    /// <summary>
    /// Returns a hash over the message type, the surface called, and how many assignable
    /// types and groups the site carries.
    /// </summary>
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
