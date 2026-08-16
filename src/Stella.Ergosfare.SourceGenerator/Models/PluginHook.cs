
namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Which pipeline point a plugin call is emitted at. Mirrors
///     <c>Stella.Ergosfare.Plugins.Abstractions.Hook</c> by numeric value — the generator
///     cannot reference that assembly, so it reads the attribute's constructor argument and
///     maps it here.
/// </summary>
/// <remarks>
///     The numeric values are a compatibility surface: a plugin compiled against one version
///     of the abstractions carries the number, not the name. They are never renumbered.
/// </remarks>
internal enum PluginHook
{
    Start = 0,
    PreMain = 1,
    PostMain = 2,
    Finish = 3,
}
