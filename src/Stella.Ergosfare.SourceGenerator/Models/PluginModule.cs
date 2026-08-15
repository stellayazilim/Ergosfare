namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Mirrors <c>Stella.Ergosfare.Plugins.Abstractions.Module</c> by numeric value; see
///     <see cref="PluginHook"/> for why the values are pinned.
/// </summary>
[Flags]
internal enum PluginModule
{
    None = 0,
    Command = 1 << 0,
    Query = 1 << 1,
    Event = 1 << 2,
    All = Command | Query | Event,
}
