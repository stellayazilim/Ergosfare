namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
///     An adapter's served slots, flattened into one string so the models stay comparable
///     by value: the slot type expressions joined by a unit separator. Reading it back is
///     pure string work, which is what lets the plan layer ask what an adapter serves
///     without a symbol in hand.
/// </summary>
internal static class AdapterSlotKey
{
    internal const char Separator = '\x1f';

    /// <summary>Whether the key names this exact slot.</summary>
    internal static bool Contains(string slotsKey, string slot)
        => (Separator + slotsKey + Separator).Contains(Separator + slot + Separator);

    /// <summary>The slots the key names, empty when it names none.</summary>
    internal static string[] Split(string slotsKey)
        => slotsKey.Length == 0 ? Array.Empty<string>() : slotsKey.Split(Separator);
}
