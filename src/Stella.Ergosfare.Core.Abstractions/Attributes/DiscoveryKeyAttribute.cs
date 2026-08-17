
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Puts a type behind one or more discovery keys, so it registers only when a registration
/// call asks for one of them.
/// </summary>
/// <remarks>
/// <para>
/// A keyed type is left out of key-less discovery (<c>RegisterGenerated()</c>) and picked
/// up by <c>RegisterGenerated("reporting")</c> or by a prefix pattern such as
/// <c>RegisterGenerated("reporting.*")</c>. To keep a type in key-less discovery while
/// still making it selectable, list <see cref="DefaultKey"/> among its keys —
/// <c>[DiscoveryKey("", "debug")]</c>.
/// </para>
/// <para>
/// Applied to an assembly, the attribute supplies the keys for every type in it that
/// declares no keys of its own.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    Inherited = false)]
public sealed class DiscoveryKeyAttribute(params string[] keys) : Attribute
{
    /// <summary>
    /// The key a type carries when it declares none — the empty string, which is what
    /// key-less registration calls select.
    /// </summary>
    public const string DefaultKey = "";

    /// <summary>
    /// The discovery keys declared for this type or assembly.
    /// </summary>
    public string[] Keys => keys;
}
