namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Assembly-level record of one dispatch site the source generator observed in the
/// assembly's own source: a distinct (static message type, dispatch surface) pair reached
/// by at least one call. The generator emitting into a composition root aggregates these
/// manifests from every referenced assembly to judge whole-closure dispatch reachability —
/// provably dead dispatches (ERGO005) and handlers no dispatch site can reach
/// (ERGO007) — without needing the referenced assemblies' syntax.
/// </summary>
/// <remarks>
/// Written by generated code; not intended to be applied by hand. The message type is
/// carried by CLR metadata name (<c>Ns.Type`1</c>, nested via <c>+</c>) so the aggregating
/// compilation can rehydrate the symbol and re-derive assignability itself — the attribute
/// stays a dumb record.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DispatchSiteAttribute(string messageTypeMetadataName, DispatchKind kind, bool opaque) : Attribute
{
    /// <summary>The CLR metadata name of the site's static message type.</summary>
    public string MessageTypeMetadataName { get; } = messageTypeMetadataName;

    /// <summary>The dispatch surface the site went through.</summary>
    public DispatchKind Kind { get; } = kind;

    /// <summary>
    /// Whether the static type proves nothing about the concrete message — the bare module
    /// marker (<c>ICommand</c>/<c>IQuery</c>/<c>IEvent</c>), <c>IMessage</c>, <c>object</c>,
    /// or an unconstrained type parameter. Opaque sites conservatively reach every message
    /// assignable to the recorded type.
    /// </summary>
    public bool Opaque { get; } = opaque;

    /// <summary>
    /// Reserved: the literal group names the site dispatches under, when the generator
    /// could prove them. Group-aware reachability judgment is a planned extension; the
    /// field exists so older manifests stay readable when it lands.
    /// </summary>
    public string[]? Groups { get; set; }
}
