namespace Stella.Ergosfare.Core.Abstractions.Strategies;

/// <summary>
/// A completed <see cref="ValueTask"/> boxed once for the whole process and reused as the
/// void pipelines' (meaningless) result object. The box is deliberate: the alternative is
/// a boxing allocation on every dispatch — or, as a static field inside a generic
/// strategy, one copy per closed message type.
/// </summary>
internal static class CompletedResultBox
{
    internal static readonly object Instance = ValueTask.CompletedTask;
}
