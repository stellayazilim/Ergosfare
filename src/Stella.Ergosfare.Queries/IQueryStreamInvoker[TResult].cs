using Stella.Ergosfare.Core;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;

/// <summary>
/// Streams a query through a pipeline closed over the query's concrete type, so the stream
/// handler is always invoked through its typed member — interface-erased streaming
/// (<c>StreamAsync(IStreamQuery&lt;T&gt;)</c>) resolves the invoker from the query's runtime
/// type. Invokers are closed once per (query type, result type) and cached; the per-call
/// cancellation token flows into a fresh strategy instance, as before.
/// </summary>
internal interface IQueryStreamInvoker<out TResult>
{
    /// <summary>
    /// Streams against the dispatch engine: the concrete machinery is known by
    /// construction, so the stream runs against the invoker-cached pipeline plan — no
    /// per-call composition lookup, no scope-resolved mediator. Grouped streams resolve
    /// their group-filtered dependencies from a last-used group-set slot.
    /// <paramref name="groupsOverride"/> carries a facade-level group filter (a
    /// <see cref="Core.Abstractions.GroupSet"/>) without a settings object; when present
    /// it takes precedence over the settings' groups.
    /// </summary>
    IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        IEnumerable<string>? groupsOverride = null);
}
