namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A result-producing message pipeline closed over one concrete message type; the
/// counterpart of <see cref="IPipelineExecutor"/>.
/// </summary>
/// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
public interface IPipelineExecutor<TResult>
{
    /// <summary>
    /// Runs the pipeline for <paramref name="message"/> and returns the result it produced.
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
    /// <returns>The result produced for <paramref name="message"/>.</returns>
    ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups);
}
