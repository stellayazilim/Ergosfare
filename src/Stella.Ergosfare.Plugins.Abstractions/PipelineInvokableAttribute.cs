using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Marks a method to be called from inside every dispatch pipeline its filters admit, at the
/// given <see cref="Hook"/>.
/// </summary>
/// <remarks>
/// <para>
/// The method observes: it reads the message and the execution context and does its own
/// work. It cannot rewrite the message or produce a result — those belong to pre- and
/// post-interceptors. What it can still do, it does through the context: <c>Abort()</c>
/// stops the pipeline, and throwing routes into the exception stages.
/// </para>
/// <para>
/// One attribute covers every kind of pipeline because no hook carries a result: the points
/// a plugin can address belong to the pipeline itself, not to what it produces.
/// </para>
/// <para>
/// Declare the method generic over the message and it is closed over the concrete type at
/// each site, so a value-typed message is not boxed. The constraint doubles as a filter: a
/// method constrained <c>where TMessage : ICacheableQuery</c> reaches only pipelines whose
/// message satisfies it, and costs nothing elsewhere because nothing is emitted there.
/// Parameters are matched by what they are, in any order — the message, the
/// <c>ErgosfareContext</c>, and anything else resolved from the dispatching provider.
/// </para>
/// <para>
/// <b>The declaring service is a singleton and cannot be anything else.</b> It is registered
/// with <c>TryAddSingleton</c>, so it is constructed once per container and a dispatch never
/// pays to build one. A hook method must therefore keep no per-dispatch state on the
/// service: every dispatch in flight shares the instance. A transient registration would not
/// help either, since two hooks are two separate resolutions and would see two different
/// objects.
/// </para>
/// <para>
/// State that must travel between hooks — a timestamp, a scope, a correlation id — belongs
/// in <c>ErgosfareContext.Items</c>, which exists per dispatch for exactly this. A
/// per-dispatch <em>dependency</em> is a different matter: declare it as a parameter and it
/// is resolved from the dispatching provider at the call site, so a scoped service reaches a
/// singleton hook without the service ever holding one.
/// </para>
/// <para>
/// The return type decides how the call is made. A <c>void</c> method is called plainly and
/// never enters an async state machine, which is what makes a counter or a log line cheap; a
/// method returning <c>ValueTask</c> is awaited. <c>void</c> alone would not be enough,
/// since it cannot be awaited and <c>async void</c> loses both completion and failure.
/// </para>
/// <para>
/// Nothing at all is emitted for a plugin that is not referenced, so an application without
/// plugins gets exactly the pipeline it had before.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [PipelineInvokable(Hook.Start)]
/// public void Began&lt;TMessage&gt;(TMessage message, ErgosfareContext context)
///     => context.Items["started"] = Stopwatch.GetTimestamp();
///
/// [PipelineInvokable(Hook.Finish)]
/// public void Ended&lt;TMessage&gt;(TMessage message, ErgosfareContext context)
///     => _duration.Record(Stopwatch.GetElapsedTime((long) context.Items["started"]).TotalMilliseconds);
/// </code>
/// </example>
/// <param name="hook">The point in the pipeline to call the method at.</param>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PipelineInvokableAttribute(Hook hook) : Attribute
{
    /// <summary>
    /// The point in the pipeline this method is called at.
    /// </summary>
    public Hook Hook => hook;
}
