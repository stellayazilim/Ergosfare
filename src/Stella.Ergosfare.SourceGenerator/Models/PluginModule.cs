namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// The generator's copy of <c>Stella.Ergosfare.Plugins.Abstractions.Module</c>.
/// </summary>
/// <remarks>
/// The values are pinned to that enum's, because the generator reads them as numbers rather
/// than through a reference; see <see cref="PluginHook"/>.
/// </remarks>
[Flags]
internal enum PluginModule
{
    /// <summary>No family.</summary>
    None = 0,

    /// <summary>Command pipelines.</summary>
    Command = 1 << 0,

    /// <summary>Query pipelines.</summary>
    Query = 1 << 1,

    /// <summary>Event broadcast pipelines.</summary>
    Event = 1 << 2,

    /// <summary>Every family.</summary>
    All = Command | Query | Event,
}
