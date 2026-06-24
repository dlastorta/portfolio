using ModularMonolith.Domain.Common;

namespace ModularMonolith.Domain.Modules.Jobs.Events;

public sealed record JobStatusChangedEvent(
    Guid JobId,
    JobStatus PreviousStatus,
    JobStatus NewStatus,
    DateTime OccurredOnUtc) : IDomainEvent;
