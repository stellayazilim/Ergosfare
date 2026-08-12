namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     How a staged result plan models its result slot's adapter — the compile-time mirror
///     of the runtime's <c>ResultAdapterBinding</c> resolution (annotation exact-slot fit
///     first, then the framework's native carriers, else none).
/// </summary>
internal enum StagedResultAdapterKind
{
    /// <summary>No adapter binds to the slot; no value-path branch is emitted at all.</summary>
    None,

    /// <summary>
    ///     The framework's own <c>Result</c>/<c>Result&lt;T&gt;</c> carrier: the branch is
    ///     a direct <c>.Exception</c> field read and a real throw materializes via the
    ///     carrier's own <c>Fail</c> — no adapter instance appears in the emitted code.
    /// </summary>
    Native,

    /// <summary>
    ///     A <c>[ResultAdapter]</c>-annotated foreign carrier: the plan instantiates the
    ///     adapter once and probes through <c>TryGetException</c>; materialization applies
    ///     only when the adapter also implements <c>IResultMaterializer&lt;TResult&gt;</c>.
    /// </summary>
    Custom,
}
