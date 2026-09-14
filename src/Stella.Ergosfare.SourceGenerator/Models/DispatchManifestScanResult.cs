using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// What the referenced assemblies' manifests add up to: the dispatch sites and
/// registrations they recorded, and two facts about how complete that picture is.
/// </summary>
/// <param name="sites">The dispatch sites the referenced assemblies recorded.</param>
/// <param name="registrationSites">The registrations they recorded.</param>
/// <param name="hasUnknownSiteAssemblies">Whether some assembly's dispatch sites are unknown.</param>
/// <param name="hasOpaqueRegistrations">Whether some registration's type is unknown.</param>
/// <remarks>
/// <para>
/// An Ergosfare-referencing assembly without a manifest — built without the generator, or
/// with one older than manifests — may hold dispatch sites this scan cannot see. While that
/// is so, nothing is reported as an unreachable handler and no handler is trimmed.
/// </para>
/// <para>
/// Likewise a registration whose type cannot be known at compile time leaves the evidence
/// of what is registered incomplete, and nothing is reported as a dead dispatch.
/// </para>
/// </remarks>
internal readonly struct DispatchManifestScanResult(
    ImmutableArray<DispatchSiteModel> sites,
    ImmutableArray<RegistrationSiteModel> registrationSites,
    bool hasUnknownSiteAssemblies,
    bool hasOpaqueRegistrations) : IEquatable<DispatchManifestScanResult>
{
    /// <summary>
    /// The result of scanning nothing.
    /// </summary>
    public static readonly DispatchManifestScanResult Empty = new(
        ImmutableArray<DispatchSiteModel>.Empty, ImmutableArray<RegistrationSiteModel>.Empty, false, false);

    /// <summary>
    /// The dispatch sites the referenced assemblies recorded.
    /// </summary>
    public ImmutableArray<DispatchSiteModel> Sites { get; } = sites;

    /// <summary>
    /// The registrations the referenced assemblies recorded.
    /// </summary>
    public ImmutableArray<RegistrationSiteModel> RegistrationSites { get; } = registrationSites;

    /// <summary>
    /// Whether some referenced assembly's dispatch sites are unknown, which silences
    /// unreachable-handler reporting and trimming.
    /// </summary>
    public bool HasUnknownSiteAssemblies { get; } = hasUnknownSiteAssemblies;

    /// <summary>
    /// Whether some registration's type is unknown, which silences dead-dispatch reporting.
    /// </summary>
    public bool HasOpaqueRegistrations { get; } = hasOpaqueRegistrations;

    /// <summary>
    /// Compares both recorded lists and both flags.
    /// </summary>
    /// <param name="other">The result to compare against.</param>
    /// <returns><c>true</c> when both describe the same scan.</returns>
    /// <remarks>
    /// Element by element, because the incremental pipeline caches on value equality and
    /// array references differ between runs.
    /// </remarks>
    public bool Equals(DispatchManifestScanResult other)
    {
        if (HasUnknownSiteAssemblies != other.HasUnknownSiteAssemblies
            || HasOpaqueRegistrations != other.HasOpaqueRegistrations
            || Sites.Length != other.Sites.Length
            || RegistrationSites.Length != other.RegistrationSites.Length)
        {
            return false;
        }

        for (var i = 0; i < Sites.Length; i++)
        {
            if (!Sites[i].Equals(other.Sites[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < RegistrationSites.Length; i++)
        {
            if (!RegistrationSites[i].Equals(other.RegistrationSites[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares against another scan result.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is an equal scan result.</returns>
    public override bool Equals(object? obj) => obj is DispatchManifestScanResult other && Equals(other);

    /// <summary>
    /// Returns a hash over the list lengths and both flags.
    /// </summary>
    public override int GetHashCode()
        => (Sites.Length * 397)
           ^ (RegistrationSites.Length * 31)
           ^ (HasUnknownSiteAssemblies ? 1 : 0)
           ^ (HasOpaqueRegistrations ? 2 : 0);
}
