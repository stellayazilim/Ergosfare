using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// The message families a plugin method applies to, as a filter on emission
/// (<see cref="PluginServiceFilterAttribute"/>).
/// </summary>
/// <remarks>
/// Family is not expressible as a generic constraint — there is no type a plugin can
/// constrain on to mean "every command" without naming the module's marker — so it gets its
/// own flag. Shape filtering stays with the constraint: a method declared
/// <c>where TMessage : ICacheableQuery</c> is emitted only into plans whose message
/// satisfies it.
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
[Flags]
public enum Module
{
    /// <summary>No family — a filter that selects nothing.</summary>
    None = 0,

    /// <summary>Command dispatch plans, both the resultless and result-returning shapes.</summary>
    Command = 1 << 0,

    /// <summary>Query dispatch plans, including streams.</summary>
    Query = 1 << 1,

    /// <summary>Event broadcast plans.</summary>
    Event = 1 << 2,

    /// <summary>Every family — the default when no family filter is declared.</summary>
    All = Command | Query | Event,
}
