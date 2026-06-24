using MediatR;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Events;

public sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notification = WrapAsNotification(domainEvent);
            await publisher.Publish(notification, cancellationToken);
        }
    }

    // Builds a closed DomainEventNotification<TConcreteEvent> so that handlers can
    // subscribe to a specific event type rather than the IDomainEvent marker.
    private static INotification WrapAsNotification(IDomainEvent domainEvent)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }
}
