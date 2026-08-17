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
/// with a public parameterless constructor. An adapter that is not can reach no generated
/// table, which is what ERGO021 reports.
/// </param>
/// <param name="Location">Where the call sits, for the diagnostics that judge it.</param>
/// <remarks>
/// The argument must be a literal <c>typeof</c> the compilation resolves, and a compilation
/// names one adapter: anything else is ERGO019 or ERGO020. Both are errors because the
/// answer this model feeds — which result types the fallback serves, and what closes an open
/// definition over each of them — is the only answer the runtime has. Whether the adapter
/// fits a given message is never judged here: a fallback that serves none of a message's
/// result types simply does not bind to it, and that message keeps throwing its failures.
/// </remarks>
internal sealed record DefaultResultAdapterSiteModel(
    bool IsOpaque,
    string BaseTypeExpression,
    bool IsOpenGeneric,
    int Arity,
    string ParameterNamesKey,
    string AdapterSlotsKey,
    string MaterializerSlotsKey,
    bool IsBakeable,
    LocationInfo? Location)
{
    /// <summary>
    /// The model of a call whose argument could not be read.
    /// </summary>
    /// <param name="location">Where the call sits.</param>
    /// <returns>The opaque model.</returns>
    public static DefaultResultAdapterSiteModel OpaqueAt(LocationInfo? location) => new(
        IsOpaque: true,
        BaseTypeExpression: string.Empty,
        IsOpenGeneric: false,
        Arity: 0,
        ParameterNamesKey: string.Empty,
        AdapterSlotsKey: string.Empty,
        MaterializerSlotsKey: string.Empty,
        IsBakeable: false,
        Location: location);

    /// <summary>
    /// Whether another call names the same adapter as this one.
    /// </summary>
    /// <param name="other">The call to compare with.</param>
    /// <returns><c>true</c> when the two agree on every read fact.</returns>
    /// <remarks>
    /// Compared field by field rather than by record equality, which would count two calls
    /// naming one adapter from different lines as a disagreement.
    /// </remarks>
    public bool NamesSameAdapterAs(DefaultResultAdapterSiteModel other)
        => IsOpaque == other.IsOpaque
           && IsOpenGeneric == other.IsOpenGeneric
           && Arity == other.Arity
           && BaseTypeExpression == other.BaseTypeExpression
           && ParameterNamesKey == other.ParameterNamesKey
           && AdapterSlotsKey == other.AdapterSlotsKey
           && MaterializerSlotsKey == other.MaterializerSlotsKey
           && IsBakeable == other.IsBakeable;
}
