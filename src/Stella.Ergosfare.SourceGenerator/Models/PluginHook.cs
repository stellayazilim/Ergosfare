
namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which point in a pipeline a plugin call is written at.
/// </summary>
/// <remarks>
/// <para>
/// The generator's copy of <c>Stella.Ergosfare.Plugins.Abstractions.Hook</c>. It cannot
/// reference that assembly, so it reads the number out of the attribute's argument and maps
/// it here.
/// </para>
/// <para>
/// The numbers are part of the compatibility surface: a plugin compiled against one version
/// of the abstractions carries the number, not the name, so they are never renumbered.
/// </para>
/// </remarks>
internal enum PluginHook
{
    /// <summary>Before anything else in the pipeline runs.</summary>
    Start = 0,

    /// <summary>Immediately before the main handler.</summary>
    PreMain = 1,

    /// <summary>Immediately after the main handler returns.</summary>
    PostMain = 2,

    /// <summary>Once the pipeline has completed.</summary>
    Finish = 3,
}
