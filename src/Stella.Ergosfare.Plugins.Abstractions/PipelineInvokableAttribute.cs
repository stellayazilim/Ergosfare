using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Marks a method whose call the generator emits directly into the dispatch plans of
/// pipelines that produce a result, at the given <see cref="Stage"/>. Resultless pipelines
/// have their own attribute — see <see cref="VoidPipelineInvokableAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// The method is an <b>observer</b>: it reads the message, the result and the execution
/// context, and does its own work. It does not rewrite the message or the result — that is
/// what pre and post interceptors are for, and a second way to do it would only be a second
/// set of semantics to keep consistent. What it can do beyond observing, it does through the
/// context: <c>Abort()</c> stops the pipeline, and throwing routes to the exception stages.
/// </para>
/// <para>
/// Declare the method generic over the message (and result) and the generator closes it over
/// the concrete types at each emission site — no boxing for value-typed messages or results,
/// and the constraint doubles as a filter: a method constrained
/// <c>where TMessage : ICacheableQuery</c> is emitted only into plans whose message
/// satisfies it, and costs nothing anywhere else because nothing is emitted there.
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
/// [PipelineInvokable(Stage.PostMainHandler)]
/// public void Count&lt;TMessage, TResult&gt;(TMessage message, TResult result, ErgosfareContext context)
///     => _handled.Add(1);
/// </code>
/// </example>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PipelineInvokableAttribute(Stage stage) : Attribute
{
    /// <summary>The plan boundary the call is emitted at.</summary>
    public Stage Stage => stage;
}
