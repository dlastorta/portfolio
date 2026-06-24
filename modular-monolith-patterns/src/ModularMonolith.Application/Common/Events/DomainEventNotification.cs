using MediatR;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Events;

/// <summary>
/// Adapts a Domain <see cref="IDomainEvent"/> into a MediatR <see cref="INotification"/>.
/// This is the seam that lets the Domain stay free of MediatR: domain events are plain
/// Domain types, and only here — in the Application layer — do they become notifications
/// that <see cref="INotificationHandler{TNotification}"/> implementations can subscribe to.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
