namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Assembly-level marker stamped by the source generator whenever it ran with dispatch-site
/// manifest support — including when the assembly contains no dispatch site at all. Its
/// presence is what lets an aggregating composition root distinguish "this assembly truly
/// dispatches nothing" from "this assembly predates manifests (or was built without the
/// generator), so its dispatch sites are unknown". Unreachable-handler judgment (ERGOSG007)
/// and compile-time handler trimming stay silent while any Ergosfare-referencing assembly
/// in the closure lacks the marker.
/// </summary>
/// <remarks>Written by generated code; not intended to be applied by hand.</remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class DispatchManifestAttribute(int version) : Attribute
{
    /// <summary>The manifest schema version the emitting generator wrote.</summary>
    public int Version { get; } = version;

    /// <summary>
    /// Whether the assembly performs registrations whose types cannot be statically known
    /// (a non-<c>typeof</c> <c>Type</c> argument, descriptor batches, or the legacy
    /// assembly scan of older packages). Coverage evidence is then incomplete by
    /// construction, so composition roots suspend dead-dispatch judgment (ERGOSG005/006)
    /// closure-wide.
    /// </summary>
    public bool HasOpaqueRegistrations { get; set; }
}
