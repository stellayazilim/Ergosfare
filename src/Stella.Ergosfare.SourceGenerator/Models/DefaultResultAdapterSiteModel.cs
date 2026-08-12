namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of one <c>UseDefaultResultAdapter(...)</c> callsite in
///     the current compilation — the compile-time view of the container's fallback
///     adapter. A literal <c>typeof</c> argument projects the adapter's served slots so
///     staged plans can bake the binding; anything else is opaque, and an opaque site (or
///     two sites disagreeing on the type) turns baking off for the whole compilation —
///     the hosting executor's adapter-identity gate then routes affected dispatches to
///     the runtime strategy, which resolves the default through the container. Fit is
///     never judged: a default that serves no slot of a message simply does not bind
///     there, and the classic try/catch semantics stand.
/// </summary>
/// <param name="IsOpaque">Whether the argument was not a resolvable <c>typeof</c> literal.</param>
/// <param name="BaseTypeExpression">
///     Closed adapters: the full type expression. Open definitions: the expression's base
///     name without the type argument list, completed per bound slot.
/// </param>
/// <param name="IsOpenGeneric">Whether the adapter is an open generic definition.</param>
/// <param name="Arity">The definition's type parameter count; 0 for closed adapters.</param>
/// <param name="ParameterNamesKey">
///     The definition's type parameter names in position order, joined with <c>\x1f</c>.
/// </param>
/// <param name="AdapterSlotsKey">
///     The carrier expressions of the adapter's <c>IResultAdapter&lt;TResult&gt;</c>
///     implementations, joined with <c>\x1f</c> — exact slot expressions for closed
///     adapters, patterns with bare type parameter names for open definitions.
/// </param>
/// <param name="MaterializerSlotsKey">
///     The same projection of the adapter's <c>IResultMaterializer&lt;TResult&gt;</c>
///     implementations.
/// </param>
/// <param name="IsBakeable">
///     Whether emitted plan code can name and construct the adapter — accessible,
///     concrete, with a public parameterless constructor. Unbakeable defaults bake
///     nothing; the runtime tier still serves them.
/// </param>
internal sealed record DefaultResultAdapterSiteModel(
    bool IsOpaque,
    string BaseTypeExpression,
    bool IsOpenGeneric,
    int Arity,
    string ParameterNamesKey,
    string AdapterSlotsKey,
    string MaterializerSlotsKey,
    bool IsBakeable)
{
    /// <summary>The one opaque-site instance; every non-literal callsite projects to it.</summary>
    public static readonly DefaultResultAdapterSiteModel Opaque = new(
        IsOpaque: true,
        BaseTypeExpression: string.Empty,
        IsOpenGeneric: false,
        Arity: 0,
        ParameterNamesKey: string.Empty,
        AdapterSlotsKey: string.Empty,
        MaterializerSlotsKey: string.Empty,
        IsBakeable: false);
}
