namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The diagnostic ids the framework marks its experimental surfaces with, one per surface,
/// so opting in takes a single documented suppression rather than one per member.
/// </summary>
public static class ExperimentalIds
{
    /// <summary>
    /// The declarative result-adapter surface: <c>[ResultAdapter]</c>,
    /// <c>[IgnoreResultAdapter]</c>, and the <c>UseDefaultResultAdapter</c> configuration
    /// with its <c>DefaultResultAdapter</c> carrier.
    /// </summary>
    /// <remarks>
    /// What the value channel does is settled; how it is declared is not, and these shapes
    /// may still change in a minor release. Opt in for a project with
    /// <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP001&lt;/NoWarn&gt;</c>, or at one site with
    /// <c>#pragma warning disable ERGOEXP001</c>.
    /// </remarks>
    public const string ResultAdapterSurface = "ERGOEXP001";

    /// <summary>
    /// The plugin surface: everything in <c>Stella.Ergosfare.Plugins.Abstractions</c> —
    /// <c>[ErgosfarePlugin]</c>, <c>[PipelineInvokable]</c>, <c>[PluginServiceFilter]</c>
    /// and the <c>Hook</c> and <c>Module</c> enums — together with the module facade the
    /// generator emits from them.
    /// </summary>
    /// <remarks>
    /// Every hook the surface names is a promise about the shape of the generated plan, and
    /// neither the list of hooks nor the way a plugin declares what it filters on is
    /// settled. Opt in for a project with
    /// <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP002&lt;/NoWarn&gt;</c>, or at one site with
    /// <c>#pragma warning disable ERGOEXP002</c>.
    /// <para>
    /// The plugin assembly references nothing on purpose, so that a new hook can ship
    /// without moving the core's version; it therefore writes this id out as a literal, and
    /// this declaration is where to look it up.
    /// </para>
    /// </remarks>
    public const string PluginSurface = "ERGOEXP002";

    /// <summary>
    /// The streaming surface: <c>ErgosfareStream&lt;TChunk&gt;</c>, the module bases over it,
    /// <c>StreamInfo</c>, and the bridges to <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <remarks>
    /// What a stream message <i>is</i> — a message whose payload arrives a chunk at a time,
    /// bounded and single-pass — is settled. What is not settled is what the pipeline does
    /// around one: which stages a stream message gets and what they are handed, how a refused
    /// dispatch reaches a caller that is still writing, and whether the chunk sequence keeps
    /// this shape once the byte path is tuned. Those answers will move, and moving them will
    /// not be source-compatible.
    /// <para>
    /// Marked experimental rather than obsolete because none of it has shipped: an error by
    /// default is the honest default for a surface nobody depends on yet. Opt in for a project
    /// with <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP003&lt;/NoWarn&gt;</c>, or at one site with
    /// <c>#pragma warning disable ERGOEXP003</c>.
    /// </para>
    /// </remarks>
    public const string StreamingSurface = "ERGOEXP003";
}
