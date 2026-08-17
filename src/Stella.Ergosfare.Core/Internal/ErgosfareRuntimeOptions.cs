namespace Stella.Ergosfare.Core.Internal;

/// <summary>
/// The runtime switches registration captured, read by the dispatch machinery afterwards.
/// </summary>
internal sealed class ErgosfareRuntimeOptions
{
    /// <summary>
    /// Whether every participant is resolved once and kept for the life of the process,
    /// whatever lifetime it was registered with.
    /// </summary>
    /// <remarks>
    /// Left at its default of <c>false</c>, registered lifetimes decide: a message whose
    /// participants are all singletons takes the memoized path, and everything else
    /// resolves from the calling scope on each dispatch.
    /// </remarks>
    public bool MemoizeAllHandlers { get; init; }
}
