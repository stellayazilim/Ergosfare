namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// The diagnostic id marking this whole assembly experimental. It is spelled here rather
/// than taken from <c>Stella.Ergosfare.Core.Abstractions.ExperimentalIds</c> because the
/// plugin surface deliberately references nothing: the generator matches its attributes by
/// metadata name, so a new stage can ship without moving the core's version. The public
/// declaration consumers look the id up in stays <c>ExperimentalIds.PluginSurface</c>, and
/// the two must not drift.
/// </summary>
internal static class ExperimentalSurface
{
    /// <summary>Suppress with <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP002&lt;/NoWarn&gt;</c>.</summary>
    internal const string Id = "ERGOEXP002";
}
