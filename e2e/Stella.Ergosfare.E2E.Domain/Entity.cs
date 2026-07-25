namespace Stella.Ergosfare.E2E.Domain;

/// <summary>
/// Base aggregate: carries a list of domain events. The events are plain objects — this
/// Domain assembly has no Ergosfare reference — collected and published by the
/// infrastructure when the unit of work is committed.
/// </summary>
public abstract class Entity
{
    private readonly List<object> _domainEvents = [];

    public IReadOnlyCollection<object> DomainEvents => _domainEvents;

    protected void Raise(object domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
