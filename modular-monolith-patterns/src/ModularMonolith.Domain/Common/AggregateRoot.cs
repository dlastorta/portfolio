namespace ModularMonolith.Domain.Common;

/// <summary>
/// An aggregate root is the consistency boundary for a cluster of entities.
/// It collects domain events as its state changes; the infrastructure layer
/// publishes them after the unit of work is saved.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
