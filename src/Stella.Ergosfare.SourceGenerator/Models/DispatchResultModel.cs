namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One result root of a dispatchable message: the closed result type of an
/// <c>ICommand&lt;T&gt;</c>/<c>IQuery&lt;T&gt;</c> contract, or of an
/// <c>IStreamQuery&lt;T&gt;</c> when <paramref name="IsStream"/> is set.
/// </summary>
/// <param name="ResultTypeExpression">Fully qualified expression of the closed result type.</param>
/// <param name="IsStream">Whether the root feeds the streaming dispatch path.</param>
internal readonly record struct DispatchResultModel(
    string ResultTypeExpression,
    bool IsStream);
