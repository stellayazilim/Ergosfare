// The executor ends a stream message's channel when its pipeline stops, which is what the
// experimental streaming surface exists for.
#pragma warning disable ERGOEXP003

using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The void pipeline of a message type no compiled plan serves: every dispatch fails, as
/// precisely as the participants allow.
/// </summary>
/// <param name="dependenciesFactory">The factory participants are resolved through.</param>
/// <param name="messageType">The message type this executor answers for.</param>
/// <remarks>
/// Holding the type as a value keeps the executor a single non-generic class, which is what
/// lets a planless type get one without reflection. Nothing is cached: the failure is
/// re-derived on every dispatch, so the exception always names the current participants.
/// </remarks>
internal sealed class UnplannedVoidExecutor(
    IMessageDependenciesFactory dependenciesFactory,
    Type messageType) : IPipelineExecutor
{
    /// <inheritdoc />
    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        // A message that carries chunks still has a channel to close; a failed dispatch that
        // leaves it open would strand the writer.
        if (message is global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream)
        {
            try
            {
                return Throw(groups);
            }
            finally
            {
                stream.EndDispatch();
            }
        }

        return Throw(groups);
    }

    /// <summary>
    /// Fails the dispatch: <see cref="Abstractions.Exceptions.NoHandlerFoundException"/>
    /// when nothing serves the message,
    /// <see cref="Abstractions.Exceptions.MultipleHandlerFoundException"/> when a level is
    /// contested, and <see cref="Abstractions.Exceptions.UnplannedDispatchException"/> for
    /// the pipeline that would have run had a plan been compiled.
    /// </summary>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>Never returns.</returns>
    private ValueTask Throw(IEnumerable<string>? groups)
    {
        // Create throws NoHandlerFoundException itself when no composition serves the
        // message — or the groups select nobody — which is the failure that case keeps.
        var dependencies = dependenciesFactory.Create(messageType, groups is null ? [] : [.. groups]);

        throw UnplannedDispatch.ForMissingPlan(messageType, dependencies);
    }
}
