
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Gates a registrable construct behind one or more discovery keys: a keyed type is
/// excluded from default discovery (<c>RegisterGenerated()</c>, <c>RegisterFromAssembly(assembly)</c>)
/// and registers only when a registration call selects one of its keys, e.g.
/// <c>RegisterGenerated("reporting")</c> or <c>RegisterGenerated("reporting.*")</c>.
/// </summary>
/// <remarks>
/// <para>
/// Untagged types carry the implicit default key (the empty string), which is what the
/// pattern-less registration calls select. Listing the empty string alongside other keys
/// (<c>[DiscoveryKey("", "debug")]</c>) keeps a type in default discovery while also making
/// it selectable by key.
/// </para>
/// <para>
/// Applied to an assembly, the attribute sets the default keys for every registrable type
/// in that assembly that declares no <see cref="DiscoveryKeyAttribute"/> of its own —
/// letting a library tag its whole surface (e.g. a modular-monolith module) in one place.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    Inherited = false)]
public sealed class DiscoveryKeyAttribute(params string[] keys) : Attribute
{
    /// <summary>
    /// The default discovery key carried by types that declare no
    /// <see cref="DiscoveryKeyAttribute"/>: the empty string, selected by the pattern-less
    /// registration calls.
    /// </summary>
    public const string DefaultKey = "";

    /// <summary>
    /// Gets the discovery keys assigned to the type or assembly.
    /// </summary>
    public string[] Keys => keys;
}
