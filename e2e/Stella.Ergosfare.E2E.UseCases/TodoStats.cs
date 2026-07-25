namespace Stella.Ergosfare.E2E.UseCases;

/// <summary>
/// Process-wide counter incremented by the event handler. Exposed over <c>GET /stats</c>
/// so the e2e assertions can observe that the event pipeline actually fired.
/// </summary>
public sealed class TodoStats
{
    private int _created;
    private int _completed;

    public int Created => Volatile.Read(ref _created);
    public int Completed => Volatile.Read(ref _completed);

    public void IncrementCreated() => Interlocked.Increment(ref _created);
    public void IncrementCompleted() => Interlocked.Increment(ref _completed);
}
