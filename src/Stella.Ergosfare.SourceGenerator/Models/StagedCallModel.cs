namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One staged interceptor call: the concrete type to resolve, the arm to invoke it
/// through, and — when the participant qualifies — the bare <c>new T(...)</c> expression
/// the direct-construction variant substitutes for the container resolution.
/// </summary>
internal readonly record struct StagedCallModel(string TypeExpression, StagedCallArm Arm, string? ConstructionExpression);
