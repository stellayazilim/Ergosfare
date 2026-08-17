using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Narrows which pipelines a plugin service's <see cref="PipelineInvokableAttribute"/>
/// methods reach. On the service it filters every method; on a method, only that one.
/// </summary>
/// <remarks>
/// <para>
/// Filtering happens while the pipeline is compiled, so a pipeline the filter excludes
/// carries no call and no runtime test — the plugin costs nothing there at all. That is what
/// separates a filter from an <c>if</c> at the top of the method.
/// </para>
/// <para>
/// There are two axes and only two. <b>Family</b> comes from <see cref="Module"/>, because
/// "every command" cannot be expressed as a generic constraint. <b>Shape</b> stays with the
/// constraint on the method itself. Filtering by what a handler injects was considered and
/// left out: it cannot see transitive dependencies, so a handler reaching a database through
/// a repository would go uncovered without saying so.
/// </para>
/// <para>
/// Filters combine: several attributes on one target intersect, and a filter on a method
/// narrows the service's rather than replacing it.
/// </para>
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PluginServiceFilterAttribute : Attribute
{
    /// <summary>
    /// Filters by message family.
    /// </summary>
    /// <param name="modules">The families to reach.</param>
    public PluginServiceFilterAttribute(Module modules)
    {
        Modules = modules;
        Keys = [];
    }

    /// <summary>
    /// Filters by discovery key, reaching only pipelines whose message carries one of the
    /// given keys.
    /// </summary>
    /// <param name="keys">
    /// The keys to match, as declared by <c>[DiscoveryKey]</c> on the message or its
    /// assembly. The empty string is the key an untagged message carries.
    /// </param>
    /// <remarks>
    /// Declaring no key filter is not the same as "every key": it selects the default key
    /// alone, exactly as a key-less <c>RegisterGenerated()</c> does. A keyed construct was
    /// deliberately kept out of default discovery by its author, and a plugin saying nothing
    /// should not put it back in. To reach keyed pipelines, name their keys — adding the
    /// empty string alongside covers both.
    /// </remarks>
    public PluginServiceFilterAttribute(params string[] keys)
    {
        Modules = Module.All;
        Keys = keys;
    }

    /// <summary>
    /// The families this filter selects; <see cref="Module.All"/> when it filters by key
    /// instead.
    /// </summary>
    public Module Modules { get; }

    /// <summary>
    /// The discovery keys this filter selects. Empty means the filter says nothing about
    /// keys, which selects the default key alone.
    /// </summary>
    public string[] Keys { get; }
}
