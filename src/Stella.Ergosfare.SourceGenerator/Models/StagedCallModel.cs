namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>One staged interceptor call: the concrete type to resolve and the arm to invoke it through.</summary>
internal readonly record struct StagedCallModel(string TypeExpression, StagedCallArm Arm);
