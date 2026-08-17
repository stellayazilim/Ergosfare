
namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One participant of a compiled composition, in the form it is written out as.
/// </summary>
/// <param name="TypeofExpression">
/// The argument to write inside <c>typeof</c>, in unbound form for a generic definition.
/// </param>
/// <param name="GroupsExpression">
/// The group array to write, or <c>null</c> when the participant declares no groups and so
/// belongs to the default one.
/// </param>
internal readonly record struct FrozenParticipantModel(string TypeofExpression, string? GroupsExpression);
