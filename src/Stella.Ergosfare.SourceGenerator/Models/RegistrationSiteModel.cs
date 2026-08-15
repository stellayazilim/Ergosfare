using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of one manual registration site: a
///     <c>Register&lt;T&gt;()</c> / <c>Register(typeof(T))</c> call. Manual registration is
///     the same source-generated collection path as <c>RegisterGenerated()</c> — per type
///     instead of in bulk — so the registered type's main-handler contracts count as
///     coverage evidence in the dead-dispatch judgment exactly like discovered ones.
///     A site whose type cannot be statically known (a non-<c>typeof</c> argument,
///     descriptor batches, or the legacy assembly scan of older packages) is opaque:
///     coverage evidence is then incomplete by construction and the judgment suspends.
/// </summary>
internal readonly struct RegistrationSiteModel : IEquatable<RegistrationSiteModel>
{
    /// <summary>
    ///     CLR metadata name of the registered type — the manifest attribute's payload —
    ///     or <c>null</c> for an opaque registration.
    /// </summary>
    public required string? TypeMetadataName { get; init; }

    /// <summary>
    ///     The registered type's main-handler message type expressions (normalized), the
    ///     coverage evidence the site contributes. Empty for plain messages, interceptors
    ///     and opaque sites.
    /// </summary>
    public required ImmutableArray<string> MainHandlerMessageKeys { get; init; }

    /// <summary>Whether the registration's type is statically unknowable.</summary>
    public required bool IsOpaque { get; init; }

    /// <summary>
    ///     Where to report ERGOSG018, set only for a <c>Register</c> call naming a type this
    ///     compilation cannot know. The other opaque shape — the generator's own
    ///     <c>RegisterParticipants</c> batch channel — leaves it null: its argument is an
    ///     <c>IEnumerable&lt;Type&gt;</c> by design, so it is opaque without being a defect.
    /// </summary>
    public required LocationInfo? UnknownTypeLocation { get; init; }

    public bool Equals(RegistrationSiteModel other)
    {
        if (TypeMetadataName != other.TypeMetadataName
            || IsOpaque != other.IsOpaque
            || !Nullable.Equals(UnknownTypeLocation, other.UnknownTypeLocation)
            || MainHandlerMessageKeys.Length != other.MainHandlerMessageKeys.Length)
        {
            return false;
        }

        for (var i = 0; i < MainHandlerMessageKeys.Length; i++)
        {
            if (!string.Equals(MainHandlerMessageKeys[i], other.MainHandlerMessageKeys[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is RegistrationSiteModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((TypeMetadataName?.GetHashCode() ?? 0) * 397)
                   ^ (MainHandlerMessageKeys.Length << 1)
                   ^ (IsOpaque ? 1 : 0);
        }
    }
}
