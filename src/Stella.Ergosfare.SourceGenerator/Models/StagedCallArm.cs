namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which contract a staged interceptor call goes through.
/// </summary>
/// <remarks>
/// Worked out at compile time from the contracts the interceptor implements, in the same
/// order the runtime strategy tests them, so the written call reaches exactly the member the
/// strategy would have.
/// </remarks>
internal enum StagedCallArm : byte
{
    /// <summary>The result-typed asynchronous contract, which the strategy tests first.</summary>
    AsyncTyped,

    /// <summary>
    /// The result-agnostic asynchronous contract, tested second — and the only asynchronous
    /// one a pre-interceptor has.
    /// </summary>
    AsyncAgnostic,

    /// <summary>The synchronous contract, tested last.</summary>
    Sync,
}
