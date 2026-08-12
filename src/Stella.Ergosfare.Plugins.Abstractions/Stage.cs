using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Where in a generated dispatch plan a <see cref="PipelineInvokableAttribute"/> method's
/// call is emitted.
/// </summary>
/// <remarks>
/// <para>
/// Each member names a structural boundary of the emitted plan, not a position in a list:
/// the plan runs its pre stages, the handler and its post stages inside one <c>try</c>,
/// routes a thrown exception to the exception stages, and runs the final stages in a
/// <c>finally</c> that an abort skips. The members below are the seams between those.
/// </para>
/// <para>
/// <b>Every stage is hosted by both plan families</b>, but a pipeline with no interceptors
/// reaches some of them differently. It is emitted as a handler plan — resolve the handler,
/// call it — where <see cref="PipelineStart"/> and <see cref="PreMainHandler"/> land on the
/// same point, as do <see cref="PostMainHandler"/> and <see cref="AfterPost"/>: with no pre
/// chain to run, "before any pre interceptor" and "immediately before the handler" are the
/// same instant, and the same holds after it.
/// </para>
/// <para>
/// <see cref="OnException"/> and <see cref="OnFinal"/> need a guard the bare handler plan
/// does not have, so declaring one makes that plan grow the <c>try</c>/<c>catch</c> or
/// <c>try</c>/<c>finally</c> it needs. Nobody declaring them means neither is emitted and the
/// plan stays bare — the cost appears only for the consumer who installed the plugin that
/// asked for it.
/// </para>
/// <para>
/// Every member here is a permanent promise about the shape of the emitted plan — a plugin
/// compiled against <see cref="PostMainHandler"/> expects the handler and the post stages to
/// stay distinguishable. The list is deliberately short for that reason; a boundary that is
/// not already part of the public pipeline contract does not belong in it.
/// </para>
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
public enum Stage
{
    /// <summary>
    /// Before any pre interceptor runs — the first thing in the pipeline's <c>try</c>.
    /// The message is the one the call site passed; no participant has rewritten it yet.
    /// </summary>
    PipelineStart = 0,

    /// <summary>
    /// After every pre interceptor, immediately before the main handler. The message is the
    /// final one — whatever the pre chain rewrote it to.
    /// </summary>
    PreMainHandler = 1,

    /// <summary>
    /// Immediately after the main handler returns, before any post interceptor. The result
    /// is the handler's own, before the post chain rewrites it. Resultless pipelines carry
    /// <c>Unit.Value</c> here.
    /// </summary>
    PostMainHandler = 2,

    /// <summary>
    /// After every post interceptor, still on the success path. The result is the one the
    /// caller will receive.
    /// </summary>
    AfterPost = 3,

    /// <summary>
    /// On the exception path, alongside the exception interceptors. Reached when a
    /// participant or the handler threw anything other than an abort — an abort cuts the
    /// pipeline and never arrives here.
    /// </summary>
    /// <remarks>
    /// A pipeline without interceptors has no exception stage of its own; declaring this
    /// stage makes its handler plan grow the <c>try</c>/<c>catch</c> that hosts the call.
    /// </remarks>
    OnException = 4,

    /// <summary>
    /// On the final path, alongside the final interceptors: runs after success and after a
    /// handled exception alike.
    /// </summary>
    /// <remarks>
    /// An <c>Abort()</c> skips this stage, exactly as it skips the final interceptors — in a
    /// grown handler plan too, so the semantics do not depend on whether the pipeline has
    /// interceptors. A plugin that must run on every path, abort included, cannot express
    /// that here; that is what the scope shape is for.
    /// </remarks>
    OnFinal = 5,
}
