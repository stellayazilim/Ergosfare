
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A void message pipeline closed over one concrete message type, built once for that type
/// and reused for every dispatch of it.
/// </summary>
/// <remarks>
/// The group filter is a per-call argument rather than part of the executor's identity: a
/// single executor serves every filter and selects the matching composition on each call.
/// </remarks>
public interface IPipelineExecutor
{
    /// <summary>
    /// Runs the pipeline for <paramref name="message"/> and completes once every
    /// participant has run.
    /// </summary>
    /// <param name="message">
    /// The message to run. Its runtime type is the executor's message type, or a type
    /// derived from it.
    /// </param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">
    /// The groups to run; <c>null</c> runs the default group.
    /// </param>
    ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups);
}
