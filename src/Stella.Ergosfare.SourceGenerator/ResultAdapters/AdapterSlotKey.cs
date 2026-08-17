namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
/// The result types an adapter serves, flattened into a single string so the models holding
/// it stay comparable by value.
/// </summary>
/// <remarks>
/// The type expressions are joined by a separator, and reading them back is plain string
/// work — which is what lets the planning layer ask what an adapter serves without holding a
/// symbol.
/// </remarks>
internal static class AdapterSlotKey
{
    /// <summary>
    /// The character joining the type expressions. A control character, so no type
    /// expression can contain it.
    /// </summary>
    internal const char Separator = '\x1f';

    /// <summary>
    /// Reports whether the key names exactly this result type.
    /// </summary>
    /// <param name="slotsKey">The flattened key.</param>
    /// <param name="slot">The result type to look for.</param>
    /// <returns><c>true</c> when the key names it.</returns>
    /// <remarks>
    /// Both sides are wrapped in separators before the search, so one type expression cannot
    /// match part of another.
    /// </remarks>
    internal static bool Contains(string slotsKey, string slot)
        => (Separator + slotsKey + Separator).Contains(Separator + slot + Separator);

    /// <summary>
    /// Returns the result types the key names.
    /// </summary>
    /// <param name="slotsKey">The flattened key.</param>
    /// <returns>The type expressions, empty when the key names none.</returns>
    internal static string[] Split(string slotsKey)
        => slotsKey.Length == 0 ? Array.Empty<string>() : slotsKey.Split(Separator);
}
