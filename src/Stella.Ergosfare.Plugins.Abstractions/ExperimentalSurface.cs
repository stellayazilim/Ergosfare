namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// The diagnostic id that marks this assembly experimental.
/// </summary>
/// <remarks>
/// Spelled out here rather than taken from
/// <c>Stella.Ergosfare.Core.Abstractions.ExperimentalIds</c>, because the plugin surface
/// references nothing on purpose: the generator matches its attributes by metadata name, so
/// a new hook can ship without moving the core's version. Consumers look the id up in
/// <c>ExperimentalIds.PluginSurface</c>, and the two must stay identical.
/// </remarks>
internal static class ExperimentalSurface
{
    /// <summary>
    /// The id, suppressed with <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP002&lt;/NoWarn&gt;</c>.
    /// </summary>
    internal const string Id = "ERGOEXP002";
}
