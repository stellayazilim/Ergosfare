namespace Stella.Ergosfare.Plugins.Outbox;

/// <summary>Process-local storage for development and tests. It does not join domain transactions.</summary>
public sealed class InMemoryOutboxStore(TimeProvider clock) : IOutboxStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Row> _rows = [];

    private sealed class Row(OutboxMessage message)
    {
        public OutboxMessage Message { get; } = message;
        public Guid Token;
        public int Attempt;
        public DateTimeOffset Available;
        public bool DeadLetter;
        public string? Error;
    }

    public ValueTask AppendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_rows.TryAdd(message.Id, new Row(message with { Payload = message.Payload.ToArray() })))
                throw new InvalidOperationException($"Outbox message '{message.Id}' already exists.");
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<OutboxLease?> ClaimAsync(TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            foreach (var row in _rows.Values)
            {
                if (row.DeadLetter || row.Available > now) continue;
                row.Token = Guid.NewGuid();
                row.Available = now + leaseDuration;
                return ValueTask.FromResult<OutboxLease?>(new(
                    row.Message with { Payload = row.Message.Payload.ToArray() }, row.Token, ++row.Attempt));
            }
        }
        return ValueTask.FromResult<OutboxLease?>(null);
    }

    public ValueTask<bool> RenewAsync(OutboxLease lease, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!Owns(lease, out var row)) return ValueTask.FromResult(false);
            row!.Available = clock.GetUtcNow() + leaseDuration;
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask CompleteAsync(OutboxLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) if (Owns(lease, out _)) _rows.Remove(lease.Message.Id);
        return ValueTask.CompletedTask;
    }

    public ValueTask FailAsync(OutboxLease lease, string error, TimeSpan retryDelay, bool deadLetter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (Owns(lease, out var row))
            {
                row!.Token = Guid.Empty;
                row.Available = clock.GetUtcNow() + retryDelay;
                row.DeadLetter = deadLetter;
                row.Error = error;
            }
        }
        return ValueTask.CompletedTask;
    }

    private bool Owns(OutboxLease lease, out Row? row)
        => _rows.TryGetValue(lease.Message.Id, out row) && row.Token == lease.Token
           && row.Available > clock.GetUtcNow();
}
