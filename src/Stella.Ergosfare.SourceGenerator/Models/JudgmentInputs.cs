namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Composition-root override, the trim opt-in, and the two compilation facts the
///     reachability judgment gates on.
/// </summary>
internal readonly record struct JudgmentInputs(
    bool? CompositionRootOverride,
    bool TrimUnusedHandlers,
    bool ScanReferences,
    bool IsExecutableOutput);
