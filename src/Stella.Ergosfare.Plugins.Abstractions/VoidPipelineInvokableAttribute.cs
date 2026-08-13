using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// The resultless counterpart of <see cref="PipelineInvokableAttribute"/>: marks a method
/// whose call the generator emits into the dispatch plans of pipelines that produce no
/// result — resultless commands and event broadcasts.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="PipelineInvokableAttribute"/> because the shapes genuinely
/// differ. A resultless pipeline has no result to observe: its slot carries
/// <c>Unit.Value</c> and nothing else, so a single attribute would force every plugin author
/// to declare a <c>TResult</c> that is always <c>Unit</c> and never read. The split follows
/// the one the pipeline contracts already make everywhere else — the post-interceptor
/// contracts, the staged plans and the dispatch roots all come in a result-typed and a
/// resultless shape.
/// </para>
/// <para>
/// Everything else matches <see cref="PipelineInvokableAttribute"/>: the method is an
/// observer, <c>void</c> is emitted as a plain call and <c>ValueTask</c> with <c>await</c>,
/// the generic is closed over the concrete message type at each emission site, and a
/// constraint on the message doubles as a filter.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [VoidPipelineInvokable(Stage.PostMainHandler)]
/// public void Count&lt;TMessage&gt;(TMessage message, ErgosfareContext context)
///     => _handled.Add(1);
/// </code>
/// </example>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class VoidPipelineInvokableAttribute(Stage stage) : Attribute
{
    /// <summary>The plan boundary the call is emitted at.</summary>
    public Stage Stage => stage;
}
