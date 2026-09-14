namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One result a dispatchable message can produce: the closed result type of an
/// <c>ICommand&lt;T&gt;</c> or <c>IQuery&lt;T&gt;</c>, or of an
/// <c>IStreamQuery&lt;T&gt;</c>.
/// </summary>
/// <param name="ResultTypeExpression">The fully qualified closed result type.</param>
/// <param name="IsStream">Whether this result is streamed rather than returned once.</param>
/// <param name="ResultIsValueType">
/// Whether the result is a value type. Emission needs to know, because casting through an
/// erased generic behaves differently for value and reference results.
/// </param>
internal readonly record struct DispatchResultModel(
    string ResultTypeExpression,
    bool IsStream,
    bool ResultIsValueType);
