namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     One group test a filtering plan evaluates once at the top of its body: the local's
///     name and the call that fills it.
/// </summary>
internal readonly record struct StagedGroupGuardModel(string Name, string Expression);
