namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One <c>UseDefaultResultAdapter(...)</c> call in this compilation — the compile-time view
/// of the container's fallback adapter.
/// </summary>
/// <param name="IsOpaque">Whether the argument was something other than a resolvable <c>typeof</c>.</param>
/// <param name="BaseTypeExpression">
/// The full type for a closed adapter; for an open definition, the name without its type
/// argument list, completed per result type it is bound to.
/// </param>
/// <param name="IsOpenGeneric">Whether the adapter is an open generic definition.</param>
/// <param name="Arity">How many type parameters the definition has; zero for a closed adapter.</param>
/// <param name="ParameterNamesKey">
/// The definition's type parameter names in order, flattened into one string.
/// </param>
/// <param name="AdapterSlotsKey">
/// The result types the adapter can read, flattened into one string: exact types for a
/// closed adapter, and patterns naming type parameters for an open definition.
/// </param>
/// <param name="MaterializerSlotsKey">
/// The same, for the result types the adapter can also build.
/// </param>
/// <param name="IsBakeable">
/// Whether generated code can name and construct the adapter — accessible, concrete, and
/// with a public parameterless constructor. An adapter that is not gets written into no
/// plan, and the runtime resolves it instead.
/// </param>
/// <remarks>
/// A literal <c>typeof</c> argument lets staged plans be compiled against the adapter.
/// Anything else is opaque, and one opaque call — or two calls naming different adapters —
/// turns that off for the whole compilation; the affected dispatches then go through the
/// general strategy, which asks the container. Whether the adapter fits a given message is
/// never judged here: a fallback that serves none of a message's result types simply does
/// not bind to it, and that message keeps throwing its failures.
/// </remarks>
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
    /// <summary>
    /// The single instance every opaque call is reduced to.
    /// </summary>
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
