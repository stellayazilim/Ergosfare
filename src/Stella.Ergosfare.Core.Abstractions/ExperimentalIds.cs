namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The diagnostic ids behind the framework's <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>
/// markings — one id per experimental surface, so consumers opt in with a single,
/// documented suppression instead of chasing members.
/// </summary>
public static class ExperimentalIds
{
    /// <summary>
    /// The declarative result-adapter surface: <c>[ResultAdapter]</c>,
    /// <c>[IgnoreResultAdapter]</c> and the <c>UseDefaultResultAdapter</c> configuration
    /// with its <c>DefaultResultAdapter</c> carrier. The value channel's semantics are
    /// settled, but this binding surface is young — shapes may still shift in a minor
    /// release. Opt in per project with <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP001&lt;/NoWarn&gt;</c>
    /// or per site with <c>#pragma warning disable ERGOEXP001</c>.
    /// </summary>
    public const string ResultAdapterSurface = "ERGOEXP001";

    /// <summary>
    /// The plugin surface: everything in <c>Stella.Ergosfare.Plugins.Abstractions</c> —
    /// <c>[ErgosfarePlugin]</c>, <c>[PipelineInvokable]</c>, <c>[VoidPipelineInvokable]</c>,
    /// <c>[PluginServiceFilter]</c> and the <c>Stage</c> and <c>Module</c> enums behind them
    /// — together with the module facade the generator emits from them. Each stage the
    /// surface names is a standing promise about the shape of the generated plan, and the
    /// list is not settled yet; the same goes for how a plugin declares what it filters on.
    /// Opt in per project with <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP002&lt;/NoWarn&gt;</c> or
    /// per site with <c>#pragma warning disable ERGOEXP002</c>.
    /// </summary>
    /// <remarks>
    /// The plugin assembly references nothing, deliberately — it carries no types the
    /// generator resolves through a reference, which is what lets a new stage ship without
    /// moving the core's version. It therefore spells this id as a literal; this declaration
    /// is where consumers look it up.
    /// </remarks>
    public const string PluginSurface = "ERGOEXP002";
}
