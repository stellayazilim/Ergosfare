using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     The referenced assemblies' aggregated dispatch manifests: dispatch sites,
///     manual-registration evidence, and two soundness flags.
///     <see cref="HasUnknownSiteAssemblies"/> guards the unreachable-handler judgment:
///     any Ergosfare-referencing assembly in the closure without a manifest marker
///     (built without the generator, or with one predating manifests) may contain
///     dispatch sites this scan cannot see, so ERGO007 and the trim stay silent
///     while it is set. <see cref="HasOpaqueRegistrations"/> guards the dead-dispatch
///     judgment: a registration whose type is statically unknowable means coverage
///     evidence is incomplete by construction, so ERGO005/006 stay silent.
/// </summary>
internal readonly struct DispatchManifestScanResult(
    ImmutableArray<DispatchSiteModel> sites,
    ImmutableArray<RegistrationSiteModel> registrationSites,
    bool hasUnknownSiteAssemblies,
    bool hasOpaqueRegistrations) : IEquatable<DispatchManifestScanResult>
{
    public static readonly DispatchManifestScanResult Empty = new(
        ImmutableArray<DispatchSiteModel>.Empty, ImmutableArray<RegistrationSiteModel>.Empty, false, false);

    public ImmutableArray<DispatchSiteModel> Sites { get; } = sites;

    public ImmutableArray<RegistrationSiteModel> RegistrationSites { get; } = registrationSites;

    public bool HasUnknownSiteAssemblies { get; } = hasUnknownSiteAssemblies;

    public bool HasOpaqueRegistrations { get; } = hasOpaqueRegistrations;

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

    public override bool Equals(object? obj) => obj is DispatchManifestScanResult other && Equals(other);

    public override int GetHashCode()
        => (Sites.Length * 397)
           ^ (RegistrationSites.Length * 31)
           ^ (HasUnknownSiteAssemblies ? 1 : 0)
           ^ (HasOpaqueRegistrations ? 2 : 0);
}
