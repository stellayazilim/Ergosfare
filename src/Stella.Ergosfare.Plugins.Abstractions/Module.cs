using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// The message families a plugin method applies to, used by
/// <see cref="PluginServiceFilterAttribute"/>.
/// </summary>
/// <remarks>
/// Family needs its own flag because it cannot be said as a generic constraint — there is no
/// type to constrain on that means "every command" without naming the module's marker.
/// Filtering by message shape stays with the constraint: a method declared
/// <c>where TMessage : ICacheableQuery</c> reaches only the pipelines whose message
/// satisfies it.
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
[Flags]
public enum Module
{
    /// <summary>
    /// No family, which selects nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// Command pipelines, both those that return a result and those that do not.
    /// </summary>
    Command = 1 << 0,

    /// <summary>
    /// Query pipelines.
    /// </summary>
    /// <remarks>
    /// Streaming queries belong to this family by name but are not served by it yet: a
    /// plugin call lives inside a compiled plan, and the streaming path has none. A stream
    /// dispatch therefore reaches no plugin until it does.
    /// </remarks>
    Query = 1 << 1,

    /// <summary>
    /// Event broadcast pipelines.
    /// </summary>
    Event = 1 << 2,

    /// <summary>
    /// Every family — what applies when no family filter is declared.
    /// </summary>
    All = Command | Query | Event,
}
