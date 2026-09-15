namespace Stella.Ergosfare.Plugins.Outbox;

/// <summary>A serialized snapshot. Contract names must remain stable across deployments.</summary>
public sealed record OutboxMessage(Guid Id, string Contract, byte[] Payload);

/// <summary>An exclusive claim; the token fences acknowledgments from older claims.</summary>
public sealed record OutboxLease(OutboxMessage Message, Guid Token, int Attempt);

/// <summary>
/// Persistence boundary. Transactional adapters stage Append in the caller's unit of work.
/// Claims and state updates must be atomic; expired claims may be reclaimed.
/// </summary>
public interface IOutboxStore
{
    ValueTask AppendAsync(OutboxMessage message, CancellationToken cancellationToken);
    ValueTask<OutboxLease?> ClaimAsync(TimeSpan leaseDuration, CancellationToken cancellationToken);
    ValueTask<bool> RenewAsync(OutboxLease lease, TimeSpan leaseDuration, CancellationToken cancellationToken);
    ValueTask CompleteAsync(OutboxLease lease, CancellationToken cancellationToken);
    ValueTask FailAsync(OutboxLease lease, string error, TimeSpan retryDelay, bool deadLetter,
        CancellationToken cancellationToken);
}
