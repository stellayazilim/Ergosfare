using System.Collections.Concurrent;

namespace Stella.Ergosfare.Plugins.Outbox;

/// <summary>Registration target for generated module initializers. No assembly or member reflection.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class GeneratedOutboxMessages
{
    private static readonly ConcurrentDictionary<Type, MessageRegistration> Registrations = new();

    public static void Register<T>(string contract, Func<T, byte[]> serialize, Func<byte[], T> deserialize)
        where T : notnull
    {
        var registration = new MessageRegistration<T>(contract, serialize, deserialize,
            Stella.Ergosfare.Core.Abstractions.GroupSet.Empty);
        if (!Registrations.TryAdd(typeof(T), registration)
            && Registrations[typeof(T)].Contract != contract)
            throw new InvalidOperationException($"Duplicate generated outbox registration for '{typeof(T)}'.");
    }

    internal static IEnumerable<MessageRegistration> All => Registrations.Values;
}
