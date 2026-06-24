using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Events;

/// <summary>
/// Publishes domain events. The Infrastructure layer calls this after a successful
/// save (see the DbContext SaveChanges override); the Application layer owns the
/// implementation because it knows how to wrap events as notifications.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
