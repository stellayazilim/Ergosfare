using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The streaming pipeline of a (query, item) pair no compiled plan serves: every stream
/// fails, as precisely as the participants allow.
/// </summary>
/// <typeparam name="TResult">The type of the items the caller asked to stream.</typeparam>
/// <param name="dependenciesFactory">The factory participants are looked up through.</param>
/// <param name="queryType">The query type this dispatch answers for.</param>
/// <remarks>
/// Generic only over the item type, which every call site names at compile time — so a
/// planless pair still gets its dispatch without reflection. The failure is raised from the
/// call itself rather than the enumeration, matching a query nothing handles.
/// </remarks>
internal sealed class UnplannedStreamDispatch<TResult>(
    IMessageDependenciesFactory dependenciesFactory,
    Type queryType) : IStreamDispatch<TResult>
{
    /// <inheritdoc />
    public IAsyncEnumerable<TResult> Stream(
        object query,
        ErgosfareContext? context,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        // Create throws NoHandlerFoundException itself when no composition serves the query
        // — a query nothing handles is a failed dispatch, not an empty stream — and the
        // analysis keeps a contested level precise before the unplanned failure.
        var dependencies = dependenciesFactory.Create(queryType, groups is null ? [] : [.. groups]);

        throw UnplannedDispatch.ForMissingPlan(queryType, dependencies);
    }
}
