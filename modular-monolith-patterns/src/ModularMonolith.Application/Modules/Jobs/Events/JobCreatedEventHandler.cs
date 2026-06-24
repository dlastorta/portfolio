using MediatR;
using Microsoft.Extensions.Logging;
using ModularMonolith.Application.Common.Events;
using ModularMonolith.Domain.Modules.Jobs.Events;

namespace ModularMonolith.Application.Modules.Jobs.Events;

/// <summary>
/// Reacts to a job being created. In a real system this is where you'd kick off a
/// welcome notification, an integration message, or an audit entry. Here it just logs —
/// the point is to show that any number of independent handlers can subscribe to one event.
/// </summary>
public sealed class JobCreatedEventHandler(ILogger<JobCreatedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<JobCreatedEvent>>
{
    public Task Handle(DomainEventNotification<JobCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        logger.LogInformation(
            "Reacting to JobCreated: {JobId} '{Title}'",
            domainEvent.JobId,
            domainEvent.Title);

        return Task.CompletedTask;
    }
}
