
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A message pipeline closed over its concrete message type, built once per message type
/// and cached process-wide. <see cref="Execute"/> receives the message as
/// <see cref="object"/> and performs a single cast to the concrete type internally, so the
/// handler is always invoked through its typed member — no object-typed bridge, no boxing
/// of the handler's <see cref="ValueTask"/>.
/// </summary>
/// <remarks>
/// The group filter is a dispatch argument, not part of the executor's identity: one
/// executor per message type serves every filter, choosing its composition per call. The
/// filter used to be baked in at construction, which meant the lookup in front of this
/// interface had to carry the group set in its key — a second dictionary and a joined
/// string key on a path that already knew the message type. The publishing and streaming
/// tables never keyed that way; this is the same shape.
/// </remarks>
/// <remarks>
/// This is the dispatch seam source-generated code will eventually implement directly;
/// the runtime builds executors reflectively (one generic instantiation per message type)
/// as the fallback.
/// </remarks>
public interface IPipelineExecutor
{
    /// <summary>
    /// Executes the void pipeline for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message instance; its runtime type is the executor's closed message type (or derived).</param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider of the scope the dispatch runs in.</param>
    /// <param name="groups">
    ///     The group filter for this dispatch, or <c>null</c> for the default pipeline.
    /// </param>
    ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups);
}
