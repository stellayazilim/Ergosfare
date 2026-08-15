using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Where in a generated dispatch plan a <see cref="PipelineInvokableAttribute"/> method's
/// call is emitted.
/// </summary>
/// <remarks>
/// <para>
/// Every member names a point that exists in <b>every</b> pipeline and costs it nothing: all
/// four are straight-line positions, so a plan never grows a <c>try</c>, a <c>catch</c> or a
/// <c>finally</c> because a plugin asked for one. A pipeline always starts, always reaches
/// its handler, and — when it completes — always ends; nothing else about its shape is
/// guaranteed.
/// </para>
/// <para>
/// The interceptor stages are deliberately not addressable. A plugin declaring itself against
/// a stage a plan does not have would either be silently dropped or force the plan to grow
/// one, and both are worse than saying that a plugin observes the pipeline rather than its
/// composition. That is also the answer for everything these four cannot see — the failure
/// path, the produced result, a point that must run on every exit: an interceptor sees them,
/// and a plugin package ships interceptors just as easily.
/// </para>
/// <para>
/// Points coincide rather than disappear. In a pipeline with no pre interceptors
/// <see cref="Start"/> and <see cref="PreMain"/> name the same instant, as do
/// <see cref="PostMain"/> and <see cref="Finish"/> with no post interceptors; both hooks
/// still run, in declaration order. In a broadcast <see cref="PreMain"/> and
/// <see cref="PostMain"/> name the seam around a <i>delivery</i>, so they run once per
/// handler.
/// </para>
/// <para>
/// The list is short because every member is a permanent promise about the shape of the
/// emitted plan. A point that is not already true of every pipeline does not belong in it.
/// </para>
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
public enum Hook
{
    /// <summary>
    /// Before anything runs — the first thing in the pipeline. The message is the one the
    /// call site passed; no participant has seen it, let alone rewritten it.
    /// </summary>
    Start = 0,

    /// <summary>
    /// Immediately before the main handler. The message is the final one — whatever the pre
    /// chain rewrote it to.
    /// </summary>
    PreMain = 1,

    /// <summary>
    /// Immediately after the main handler returns, before any post interceptor.
    /// </summary>
    PostMain = 2,

    /// <summary>
    /// The pipeline is done and control is returning to the call site.
    /// </summary>
    /// <remarks>
    /// The end of a pipeline that completed. A failure leaves through the exception path and
    /// an <c>Abort()</c> cuts the pipeline, and neither arrives here — running on those paths
    /// would take a <c>finally</c> the plan does not otherwise have. A plugin that must close
    /// something on every path registers a final interceptor, which is the participant whose
    /// whole definition is that.
    /// </remarks>
    Finish = 3,
}
