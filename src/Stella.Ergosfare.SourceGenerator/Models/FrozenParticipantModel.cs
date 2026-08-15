using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>One participant row of a frozen composition, ready for emission.</summary>
/// <param name="TypeofExpression">The participant's <c>typeof</c> argument (unbound form for definitions).</param>
/// <param name="GroupsExpression">The emitted group-array expression, or <c>null</c> for the default group alone.</param>
internal readonly record struct FrozenParticipantModel(string TypeofExpression, string? GroupsExpression);
