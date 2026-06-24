using ModularMonolith.Application.Common.Events;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.UnitTests.Common;

/// <summary>Test double that records dispatched events instead of publishing them.</summary>
public sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = [];

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        Dispatched.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}
