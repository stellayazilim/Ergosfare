namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Records one dispatch site the source generator saw in an assembly's own source: a
/// distinct pair of static message type and mediator surface that at least one call
/// reached.
/// </summary>
/// <remarks>
/// A generator running in a composition root reads these records from every referenced
/// assembly to judge which dispatches and handlers the whole program can reach — reporting
/// dispatches nothing can handle (ERGO005) and handlers nothing dispatches to (ERGO007) —
/// without needing those assemblies' source.
/// <para>
/// Written by generated code; do not apply it by hand. The message type is recorded as a
/// CLR metadata name (<c>Ns.Type`1</c>, nested types joined with <c>+</c>) so the reading
/// compilation can look the symbol up and work out assignability for itself.
/// </para>
/// </remarks>
/// <param name="messageTypeMetadataName">The metadata name of the site's static message type.</param>
/// <param name="kind">The mediator surface the site called.</param>
/// <param name="opaque">Whether the static type proves nothing about the concrete message.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DispatchSiteAttribute(string messageTypeMetadataName, DispatchKind kind, bool opaque) : Attribute
{
    /// <summary>
    /// The CLR metadata name of the site's static message type.
    /// </summary>
    public string MessageTypeMetadataName { get; } = messageTypeMetadataName;

    /// <summary>
    /// The mediator surface the site called.
    /// </summary>
    public DispatchKind Kind { get; } = kind;

    /// <summary>
    /// Whether the static message type says nothing about which message is dispatched —
    /// a bare module marker such as <c>ICommand</c>, or <c>IMessage</c>, <c>object</c>, or
    /// an unconstrained type parameter. Such a site is taken to reach every message
    /// assignable to the recorded type.
    /// </summary>
    public bool Opaque { get; } = opaque;

    /// <summary>
    /// The literal group names the site dispatches under, where they could be proven.
    /// </summary>
    /// <remarks>
    /// Reachability judgment does not consider groups today, so nothing writes or reads
    /// this. The property exists so that manifests written now stay readable if it does.
    /// </remarks>
    public string[]? Groups { get; set; }
}
