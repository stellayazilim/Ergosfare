
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Why a dispatch had no compiled plan to run; carried by
/// <see cref="UnplannedDispatchException"/> so callers and tests can tell the cases apart
/// without parsing the message.
/// </summary>
public enum UnplannedDispatchReason
{
    /// <summary>
    /// The generator produced nothing for the message type — it never saw the type, or saw
    /// it and could not model its pipeline.
    /// </summary>
    NoCompiledPlan,

    /// <summary>
    /// The generator produced no dispatch root for the message type, so the dispatch could
    /// not even be constructed for it.
    /// </summary>
    NoDispatchRoot,

    /// <summary>
    /// A plan exists, but the live pipeline is not the one it was compiled against — a
    /// participant registered at runtime, a missing one, or a changed order.
    /// </summary>
    CompositionDiverged,

    /// <summary>
    /// The pipeline memoizes participant instances, and a compiled plan resolves or
    /// constructs its participants fresh — the two contracts cannot both hold.
    /// </summary>
    MemoizedInstances,

    /// <summary>
    /// A result adapter is bound that the plan was not compiled against.
    /// </summary>
    UnplannedResultAdapter,

    /// <summary>
    /// The container uses a dependencies factory the engine does not know, so nothing the
    /// plan was compiled against can be verified.
    /// </summary>
    ForeignDependenciesFactory,

    /// <summary>
    /// The dispatch named a group set that no compiled plan serves and the filtering plan
    /// cannot be verified for.
    /// </summary>
    UnplannedGroupSet,
}
