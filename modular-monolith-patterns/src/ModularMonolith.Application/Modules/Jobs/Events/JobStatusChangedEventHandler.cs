using MediatR;
using Microsoft.Extensions.Logging;
using ModularMonolith.Application.Common.Events;
using ModularMonolith.Domain.Modules.Jobs.Events;

namespace ModularMonolith.Application.Modules.Jobs.Events;

public sealed class JobStatusChangedEventHandler(ILogger<JobStatusChangedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<JobStatusChangedEvent>>
{
    public Task Handle(DomainEventNotification<JobStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        logger.LogInformation(
            "Job {JobId} moved {PreviousStatus} -> {NewStatus}",
            domainEvent.JobId,
            domainEvent.PreviousStatus,
            domainEvent.NewStatus);

        return Task.CompletedTask;
    }
}
