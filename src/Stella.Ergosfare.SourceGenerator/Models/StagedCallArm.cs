namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     The pattern-match arm the runtime invocation strategy would select for one staged
///     interceptor call — determined at compile time from the interceptor's implemented
///     contracts, so the emitted call invokes exactly the member the strategy would.
/// </summary>
internal enum StagedCallArm : byte
{
    /// <summary>The result-typed asynchronous contract (first arm of the runtime pattern match).</summary>
    AsyncTyped,

    /// <summary>The result-agnostic asynchronous contract (second arm; also the sole async pre arm).</summary>
    AsyncAgnostic,

    /// <summary>The synchronous contract (last arm).</summary>
    Sync,
}
