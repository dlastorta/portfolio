namespace ModularMonolith.Domain.Common;

/// <summary>
/// Marker for something that happened in the domain and is worth reacting to.
/// Deliberately defined here, in the Domain, with no dependency on MediatR or any
/// other framework. The Application layer adapts these into notifications when it
/// dispatches them — that keeps the Domain free of outward dependencies.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
