namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of a message's <c>[ResultAdapter]</c> annotation (its own
///     or an inherited one): the adapter type, the result slots it can serve, and whether
///     the generator can instantiate and reference it. Feeds the staged plans' baked
///     adapter binding and the ERGO011 judgment.
/// </summary>
/// <param name="TypeofExpression">The adapter's fully qualified <c>typeof</c>/<c>new</c> expression.</param>
/// <param name="DisplayName">Human-readable adapter type name used in diagnostics.</param>
/// <param name="AdapterSlotsKey">
///     The result-type expressions of the adapter's <c>IResultAdapter&lt;TResult&gt;</c>
///     implementations, joined with <c>\x1f</c> — the slots the annotation can bind to.
/// </param>
/// <param name="MaterializerSlotsKey">
///     The result-type expressions of the adapter's <c>IResultMaterializer&lt;TResult&gt;</c>
///     implementations, joined with <c>\x1f</c>.
/// </param>
/// <param name="IsInstantiable">
///     Whether the runtime binding's <c>Activator.CreateInstance</c> would succeed: a
///     concrete, fully closed type with a public parameterless constructor.
/// </param>
/// <param name="IsBakeable">
///     Whether emitted plan code can additionally name and construct the type —
///     <see cref="IsInstantiable"/> plus accessibility from generated code. An annotation
///     that fits but cannot be baked disqualifies the staged plan; the runtime mirror still
///     serves the pipeline through reflection.
/// </param>
/// <param name="FitsDeclaredSlot">
///     Whether the adapter serves at least one of the message's runtime-probed result
///     slots (declared results, <c>Unit</c> for a void command, the enumerator slot for a
///     stream). The ERGO011 fit judgment.
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
    private const char Separator = '\x1f';

    /// <summary>Whether the adapter implements <c>IResultAdapter&lt;TResult&gt;</c> for the given slot.</summary>
    public bool Fits(string resultTypeExpression) => ContainsSlot(AdapterSlotsKey, resultTypeExpression);

    /// <summary>Whether the adapter implements <c>IResultMaterializer&lt;TResult&gt;</c> for the given slot.</summary>
    public bool Materializes(string resultTypeExpression) => ContainsSlot(MaterializerSlotsKey, resultTypeExpression);

    private static bool ContainsSlot(string key, string slot)
        => (Separator + key + Separator).Contains(Separator + slot + Separator);
}
