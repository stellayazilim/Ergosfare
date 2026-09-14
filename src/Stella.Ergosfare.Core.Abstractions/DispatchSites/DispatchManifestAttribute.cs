namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Marks an assembly whose dispatch sites the source generator recorded — including an
/// assembly that dispatches nothing at all.
/// </summary>
/// <remarks>
/// The marker is what separates "this assembly truly dispatches nothing" from "this
/// assembly's dispatch sites are unknown", which is the case for anything built before
/// manifests existed or without the generator. While any assembly referencing Ergosfare in
/// the program lacks the marker, unreachable-handler reporting (ERGO007) and compile-time
/// handler trimming stay off.
/// <para>Written by generated code; do not apply it by hand.</para>
/// </remarks>
/// <param name="version">The manifest schema version the generator wrote.</param>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class DispatchManifestAttribute(int version) : Attribute
{
    /// <summary>
    /// The manifest schema version the generator wrote.
    /// </summary>
    public int Version { get; } = version;

    /// <summary>
    /// Whether the assembly registers types that cannot be known at compile time — a
    /// <c>Type</c> argument that is not a <c>typeof</c>, a batch of descriptors, or the
    /// assembly scan older packages used.
    /// </summary>
    /// <remarks>
    /// Evidence of what is registered is then incomplete, so composition roots stop
    /// reporting dead dispatches (ERGO005 and ERGO006) across the whole program.
    /// </remarks>
    public bool HasOpaqueRegistrations { get; set; }
}
