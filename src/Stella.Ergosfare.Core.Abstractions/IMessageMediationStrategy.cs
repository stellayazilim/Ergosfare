
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Runs the participants of one message through a particular execution pattern — a single
/// handler, a broadcast to every subscriber, a stream — and produces the pipeline's result.
/// </summary>
/// <typeparam name="TMessage">The message type this strategy mediates.</typeparam>
/// <typeparam name="TMessageResult">The result type this strategy produces.</typeparam>
/// <remarks>
/// A strategy decides how many main handlers may run, what happens when none or several
/// match, and in what order the pre-, post-, exception- and final-interceptor stages are
/// invoked around them.
/// </remarks>
public interface IMessageMediationStrategy<in TMessage, out TMessageResult>
    where TMessage : notnull
{
    /// <summary>
    /// Runs <paramref name="message"/> through this strategy's pattern.
    /// </summary>
    /// <param name="message">The message to mediate.</param>
    /// <param name="messageDependencies">
    /// The participants resolved for the message, per stage and in invocation order.
    /// </param>
    /// <param name="executionContext">
    /// The execution context for this dispatch. It carries data between participants and
    /// takes no part in resolving them.
    /// </param>
    /// <param name="serviceProvider">
    /// The provider of the scope the dispatch runs in. The strategy resolves each
    /// participant instance from it at the point of invocation.
    /// </param>
    /// <returns>The result of running the pattern.</returns>
    TMessageResult Mediate(TMessage message, IMessageDependencies messageDependencies, ErgosfareContext executionContext, IServiceProvider serviceProvider);
}
