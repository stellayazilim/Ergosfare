using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Plugins.Outbox;

public sealed class OutboxOptions
{
    private readonly Dictionary<Type, MessageRegistration> _types = [];
    private readonly Dictionary<string, MessageRegistration> _contracts = new(StringComparer.Ordinal);
    public OutboxOptions()
    {
        foreach (var registration in GeneratedOutboxMessages.All) Add(registration);
    }

    private void Add(MessageRegistration registration)
    {
        if (_types.ContainsKey(registration.MessageType) || _contracts.ContainsKey(registration.Contract))
            throw new InvalidOperationException($"Outbox message types and contract names must be unique: '{registration.Contract}'.");
        _types.Add(registration.MessageType, registration);
        _contracts.Add(registration.Contract, registration);
    }
    internal Action<IServiceCollection>? RegisterStore { get; private set; }
    public int MaxConcurrency { get; set; } = 4;
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    public OutboxOptions UseInMemory()
        => UseStore(services => services.AddSingleton<IOutboxStore, InMemoryOutboxStore>());

    /// <summary>Installs a provider. Scoped providers may participate in the caller's transaction.</summary>
    public OutboxOptions UseStore(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        if (RegisterStore is not null) throw new InvalidOperationException("Choose exactly one outbox store.");
        RegisterStore = register;
        return this;
    }

    /// <summary>Registers AOT-safe serialization and typed event dispatch for a stable contract.</summary>
    public OutboxOptions Register<T>(string contract, JsonTypeInfo<T> json, GroupSet? groups = null)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        ArgumentNullException.ThrowIfNull(json);
        if (_types.ContainsKey(typeof(T)) || _contracts.ContainsKey(contract))
            throw new InvalidOperationException("Outbox message types and contract names must be unique.");
        var registration = new MessageRegistration<T>(contract,
            message => JsonSerializer.SerializeToUtf8Bytes(message, json),
            payload => JsonSerializer.Deserialize(payload, json)
                ?? throw new InvalidOperationException($"Null payload for '{contract}'."), groups ?? GroupSet.Empty);
        _types.Add(typeof(T), registration);
        _contracts.Add(contract, registration);
        return this;
    }

    internal MessageRegistration Find(Type type) => _types.TryGetValue(type, out var value) ? value
        : throw new InvalidOperationException($"Register outbox serialization for '{type}' in AddOutboxPlugin.");
    internal MessageRegistration Find(string contract) => _contracts.TryGetValue(contract, out var value) ? value
        : throw new InvalidOperationException($"Unknown outbox contract '{contract}'.");

    internal OutboxOptions Snapshot()
    {
        if (RegisterStore is null) throw new InvalidOperationException("AddOutboxPlugin requires a store. Select UseInMemory or a persistence adapter.");
        if (MaxConcurrency < 1 || MaxAttempts < 1 || PollInterval <= TimeSpan.Zero
            || RetryDelay < TimeSpan.Zero || LeaseDuration < TimeSpan.FromMilliseconds(30)
            || PollInterval.TotalMilliseconds > int.MaxValue || LeaseDuration.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(OutboxOptions), "Invalid outbox concurrency, attempts or timing settings.");
        var copy = new OutboxOptions
        {
            RegisterStore = RegisterStore, MaxConcurrency = MaxConcurrency, MaxAttempts = MaxAttempts,
            PollInterval = PollInterval, RetryDelay = RetryDelay, LeaseDuration = LeaseDuration
        };
        copy._types.Clear();
        copy._contracts.Clear();
        foreach (var entry in _types) copy._types.Add(entry.Key, entry.Value);
        foreach (var entry in _contracts) copy._contracts.Add(entry.Key, entry.Value);
        return copy;
    }
}

internal abstract class MessageRegistration(string contract)
{
    public string Contract { get; } = contract;
    public abstract Type MessageType { get; }
    public abstract byte[] Serialize(object message);
    public abstract ValueTask DispatchAsync(byte[] payload, IEventMediator mediator, CancellationToken cancellationToken);
}

internal sealed class MessageRegistration<T>(string contract, Func<T, byte[]> serialize, Func<byte[], T> deserialize, GroupSet groups)
    : MessageRegistration(contract) where T : notnull
{
    public override Type MessageType => typeof(T);
    public override byte[] Serialize(object message) => serialize((T)message);
    public override ValueTask DispatchAsync(byte[] payload, IEventMediator mediator, CancellationToken cancellationToken)
    {
        var message = deserialize(payload);
        // The mediator rents a fresh context; neither request context nor DI scope travels in storage.
        return mediator.PublishAsync<OutboxEvent<T>>(new(message), groups, cancellationToken);
    }
}
