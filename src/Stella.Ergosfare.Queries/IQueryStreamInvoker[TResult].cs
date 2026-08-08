using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
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
    IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IEnumerable<string>? groupsOverride = null);

    /// <summary>
    /// Engine-backed streaming: the concrete dispatch machinery is known by construction,
    /// so the stream runs against the invoker-cached, registry-version-guarded pipeline
    /// plan — no <c>MediateOptions</c>, no per-call descriptor lookup, no scope-resolved
    /// mediator. Grouped streams resolve the same group-filtered dependencies the Mediate
    /// path would build, from a last-used group-set slot.
    /// <paramref name="groupsOverride"/> carries a facade-level group filter (a
    /// <see cref="Core.Abstractions.GroupSet"/>) without a settings object; when present
    /// it takes precedence over the settings' groups.
    /// </summary>
    IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IEnumerable<string>? groupsOverride = null);
}
