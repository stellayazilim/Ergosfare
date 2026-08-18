// The executor ends a stream message's channel when its pipeline stops, which is what the
// experimental streaming surface exists for.
#pragma warning disable ERGOEXP003

using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The result-producing pipeline of a (message, result) pair no compiled plan serves; the
/// counterpart of <see cref="UnplannedVoidExecutor"/>.
/// </summary>
/// <typeparam name="TResult">The result type the caller asked for.</typeparam>
/// <param name="dependenciesFactory">The factory participants are resolved through.</param>
/// <param name="messageType">The message type this executor answers for.</param>
/// <remarks>
/// Generic only over the result type, which every call site names at compile time — so a
/// planless pair still gets its executor without reflection.
/// </remarks>
internal sealed class UnplannedResultExecutor<TResult>(
    IMessageDependenciesFactory dependenciesFactory,
    Type messageType) : IPipelineExecutor<TResult>
{
    /// <inheritdoc />
    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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

    /// <inheritdoc cref="UnplannedVoidExecutor.Throw"/>
    private ValueTask<TResult> Throw(IEnumerable<string>? groups)
    {
        // Create throws NoHandlerFoundException itself when no composition serves the
        // message — or the groups select nobody — which is the failure that case keeps.
        var dependencies = dependenciesFactory.Create(messageType, groups is null ? [] : [.. groups]);

        throw UnplannedDispatch.ForMissingPlan(messageType, dependencies);
    }
}
