using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Marks a method whose call the generator emits directly into every dispatch plan the
/// method's filters admit, at the given <see cref="Hook"/>.
/// </summary>
/// <remarks>
/// <para>
/// The method is an <b>observer</b>: it reads the message and the execution context, and
/// does its own work. It does not rewrite the message or produce a result — that is what pre
/// and post interceptors are for, and a second way to do it would only be a second set of
/// semantics to keep consistent. What it can do beyond observing, it does through the
/// context: <c>Abort()</c> stops the pipeline, and throwing routes to the exception stages.
/// </para>
/// <para>
/// One attribute serves every pipeline, resultless or result-producing, because no hook
/// carries a result: the three points a plugin can address are properties of the pipeline
/// itself, not of what it produces.
/// </para>
/// <para>
/// Declare the method generic over the message and the generator closes it over the concrete
/// type at each emission site — no boxing for value-typed messages, and the constraint
/// doubles as a filter: a method constrained <c>where TMessage : ICacheableQuery</c> is
/// emitted only into plans whose message satisfies it, and costs nothing anywhere else
/// because nothing is emitted there.
/// </para>
/// <para>
/// Parameters are bound by what they are, in any order: the message type parameter, the
/// <c>ErgosfareContext</c>, and anything else from the dispatching provider.
/// </para>
/// <para>
/// <b>The declaring service is a singleton, and that is not configurable.</b> The generated
/// module registers it with <c>TryAddSingleton</c>, so it is resolved once per container and
/// a dispatch never pays for constructing one. Which means a hook method must not keep
/// per-dispatch state on the service: every dispatch in flight shares the instance. Two hooks
/// are also two separate resolutions, so a transient registration would not rescue it — the
/// second hook would get a different object than the first.
/// </para>
/// <para>
/// State that has to travel between hooks — a timestamp, a scope, a correlation id — belongs
/// in <c>ErgosfareContext.Items</c>, which is created per dispatch and is the pipeline's own
/// channel for exactly this. A per-dispatch <i>dependency</i> is a different question and has
/// its own answer: declare it as a parameter, and it is resolved from the dispatching
/// provider at the call site, so a scoped service reaches a singleton hook without the
/// service ever capturing one.
/// </para>
/// <para>
/// The return type decides how the call is emitted. A <c>void</c> method is emitted as a
/// plain call and never enters an async state machine — which is what makes a synchronous
/// counter or a log line genuinely cheap. A method returning <c>ValueTask</c> is emitted
/// with <c>await</c>. <c>void</c> alone would not do: it is not awaitable, and
/// <c>async void</c> loses both the completion and the exception.
/// </para>
/// <para>
/// Nothing is emitted for a plugin that is not referenced, so a consumer with no plugins
/// gets the plan it would have had before this attribute existed.
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
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PipelineInvokableAttribute(Hook hook) : Attribute
{
    /// <summary>The pipeline point the call is emitted at.</summary>
    public Hook Hook => hook;
}
