namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// What decides whether this compilation judges dispatch reachability, and how far.
/// </summary>
/// <param name="CompositionRootOverride">
/// What the project said about being a composition root, or <c>null</c> to let the
/// compilation's own shape decide.
/// </param>
/// <param name="TrimUnusedHandlers">Whether the project opted into dropping unreachable handlers.</param>
/// <param name="ScanReferences">Whether referenced assemblies are scanned as well as this one.</param>
/// <param name="IsExecutableOutput">Whether this compilation produces an application rather than a library.</param>
internal readonly record struct JudgmentInputs(
    bool? CompositionRootOverride,
    bool TrimUnusedHandlers,
    bool ScanReferences,
    bool IsExecutableOutput);
