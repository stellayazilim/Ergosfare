namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A message's <c>[ResultAdapter]</c> annotation, its own or an inherited one, reduced to
/// what planning and diagnostics need.
/// </summary>
/// <param name="TypeofExpression">The adapter's fully qualified type.</param>
/// <param name="DisplayName">The adapter as a diagnostic would name it.</param>
/// <param name="AdapterSlotsKey">
/// The result types the adapter can read, flattened into one string. These are the types
/// the annotation can bind to.
/// </param>
/// <param name="MaterializerSlotsKey">
/// The result types the adapter can also build, flattened the same way.
/// </param>
/// <param name="IsInstantiable">
/// Whether the runtime could construct the adapter: concrete, fully closed, and with a
/// public parameterless constructor.
/// </param>
/// <param name="IsBakeable">
/// Whether generated code can name and construct it too — <see cref="IsInstantiable"/> and
/// accessible from generated code. An adapter that fits but cannot be named means no staged
/// plan for that message; the runtime still binds it reflectively and the pipeline behaves
/// the same.
/// </param>
/// <param name="FitsDeclaredSlot">
/// Whether the adapter serves at least one result type the message actually has — a
/// declared result, the empty value of a resultless command, or the enumerator of a stream.
/// This is what ERGO011 reports on.
/// </param>
internal sealed record ResultAdapterModel(
    string TypeofExpression,
    string DisplayName,
    string AdapterSlotsKey,
    string MaterializerSlotsKey,
    bool IsInstantiable,
    bool IsBakeable,
    bool FitsDeclaredSlot)
{
    /// <summary>
    /// The character joining the result types in a flattened key. A control character, so
    /// no type expression can contain it.
    /// </summary>
    private const char Separator = '\x1f';

    /// <summary>
    /// Reports whether the adapter can read this result type.
    /// </summary>
    /// <param name="resultTypeExpression">The result type to ask about.</param>
    /// <returns><c>true</c> when the adapter serves it.</returns>
    public bool Fits(string resultTypeExpression) => ContainsSlot(AdapterSlotsKey, resultTypeExpression);

    /// <summary>
    /// Reports whether the adapter can also build this result type from a failure.
    /// </summary>
    /// <param name="resultTypeExpression">The result type to ask about.</param>
    /// <returns><c>true</c> when the adapter can build it.</returns>
    public bool Materializes(string resultTypeExpression) => ContainsSlot(MaterializerSlotsKey, resultTypeExpression);

    /// <summary>
    /// Reports whether a flattened key names exactly this result type.
    /// </summary>
    /// <param name="key">The flattened key.</param>
    /// <param name="slot">The result type to look for.</param>
    /// <returns><c>true</c> when the key names it.</returns>
    /// <remarks>
    /// Both sides are wrapped in separators first, so one type cannot match part of
    /// another.
    /// </remarks>
    private static bool ContainsSlot(string key, string slot)
        => (Separator + key + Separator).Contains(Separator + slot + Separator);
}
