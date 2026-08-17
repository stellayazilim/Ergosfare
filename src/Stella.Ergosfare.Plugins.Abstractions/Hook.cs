using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Where in a dispatch a <see cref="PipelineInvokableAttribute"/> method is called.
/// </summary>
/// <remarks>
/// <para>
/// All four points exist in every pipeline and none of them costs it anything: each is a
/// position on the straight-line path, so a plan never gains a <c>try</c>, a <c>catch</c> or
/// a <c>finally</c> because a plugin asked for one. Every pipeline starts, reaches its
/// handler, and — when it completes — ends; nothing else about its shape is promised.
/// </para>
/// <para>
/// The interceptor stages cannot be addressed. A plugin aimed at a stage a given pipeline
/// does not have would either be dropped silently or force that stage into existence.
/// Anything these four cannot see — the failure path, the result, a point that runs on every
/// exit — is what an interceptor is for, and a plugin package can ship interceptors too.
/// </para>
/// <para>
/// Points coincide rather than disappear: with no pre-interceptors, <see cref="Start"/> and
/// <see cref="PreMain"/> name the same instant and both run, and likewise
/// <see cref="PostMain"/> and <see cref="Finish"/> with no post-interceptors. In a broadcast,
/// <see cref="PreMain"/> and <see cref="PostMain"/> surround each <em>delivery</em>, so they
/// run once per handler.
/// </para>
/// </remarks>
[Experimental(ExperimentalSurface.Id)]
public enum Hook
{
    /// <summary>
    /// Before anything else runs. The message is the one the call site passed, untouched by
    /// any participant.
    /// </summary>
    Start = 0,

    /// <summary>
    /// Immediately before the main handler, with the message as the pre-interceptors left
    /// it.
    /// </summary>
    PreMain = 1,

    /// <summary>
    /// Immediately after the main handler returns, before any post-interceptor.
    /// </summary>
    PostMain = 2,

    /// <summary>
    /// The pipeline has completed and control is returning to the call site.
    /// </summary>
    /// <remarks>
    /// Only reached by a pipeline that completed. A failure leaves through the exception
    /// path and <c>Abort()</c> cuts the pipeline short, and neither arrives here — running on
    /// those paths would need a <c>finally</c> the plan does not otherwise have. To close
    /// something on every path, register a final interceptor.
    /// </remarks>
    Finish = 3,
}
