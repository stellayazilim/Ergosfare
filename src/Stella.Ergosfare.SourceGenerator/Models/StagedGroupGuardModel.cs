namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One group test a filtering plan runs once at the top of its body, so each guarded call
/// below reads a local instead of testing again.
/// </summary>
/// <param name="Name">The local the test's answer is stored in.</param>
/// <param name="Expression">The call that computes it.</param>
internal readonly record struct StagedGroupGuardModel(string Name, string Expression);
