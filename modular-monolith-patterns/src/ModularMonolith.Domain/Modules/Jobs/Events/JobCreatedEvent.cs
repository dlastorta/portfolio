using ModularMonolith.Domain.Common;

namespace ModularMonolith.Domain.Modules.Jobs.Events;

public sealed record JobCreatedEvent(Guid JobId, string Title, DateTime OccurredOnUtc) : IDomainEvent;
