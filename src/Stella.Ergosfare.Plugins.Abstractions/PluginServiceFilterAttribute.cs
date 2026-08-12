using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Narrows which dispatch plans a plugin service's <see cref="PipelineInvokableAttribute"/>
/// methods are emitted into. Applied to the service, it filters every method on it; applied
/// to a method, only that one.
/// </summary>
/// <remarks>
/// <para>
/// Filtering is an emission-time decision, so a plan the filter excludes carries no call and
/// no runtime check — the plugin costs exactly nothing there. This is the difference between
/// a filter and an <c>if</c> at the top of the method body.
/// </para>
/// <para>
/// Two axes, deliberately only two. <b>Family</b> comes from <see cref="Module"/>, because
/// "every command" cannot be said as a generic constraint. <b>Shape</b> stays with the
/// constraint on the method itself. A third axis — filtering by what the handler injects —
/// was considered and left out: it cannot see transitive dependencies, so a handler reaching
/// the database through a repository would silently go uncovered.
/// </para>
/// <para>
/// Filters stack: several attributes on the same target intersect, and a filter on the
/// method narrows the one on the service rather than replacing it.
/// </para>
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PluginServiceFilterAttribute : Attribute
{
    /// <summary>Filters by message family.</summary>
    /// <param name="modules">The families to emit into.</param>
    public PluginServiceFilterAttribute(Module modules)
    {
        Modules = modules;
        Keys = [];
    }

    /// <summary>
    /// Filters by discovery key: the plugin is emitted only into plans whose message carries
    /// one of the given keys.
    /// </summary>
    /// <param name="keys">
    /// The discovery keys to match, as declared by <c>[DiscoveryKey]</c> on the message or
    /// its assembly. The empty string is the default key that untagged messages carry.
    /// </param>
    /// <remarks>
    /// Declaring no key filter is <b>not</b> "every key": it selects the default key alone,
    /// exactly as a pattern-less <c>RegisterGenerated()</c> does. A keyed construct is opted
    /// out of default discovery by its author, and a plugin should not opt it back in by
    /// saying nothing. To cover keyed plans, name their keys — or include the empty string
    /// alongside them to cover both.
    /// </remarks>
    public PluginServiceFilterAttribute(params string[] keys)
    {
        Modules = Module.All;
        Keys = keys;
    }

    /// <summary>The families this filter selects; <see cref="Module.All"/> when unfiltered.</summary>
    public Module Modules { get; }

    /// <summary>
    /// The discovery keys this filter selects; empty when the filter says nothing about
    /// keys, which selects the default key alone.
    /// </summary>
    public string[] Keys { get; }
}
