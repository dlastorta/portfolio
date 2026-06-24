using Microsoft.EntityFrameworkCore;
using ModularMonolith.Application.Common.Events;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Catalog;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventDispatcher domainEventDispatcher)
    : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<CrewRole> CrewRoles => Set<CrewRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Persists changes, then publishes any domain events the saved aggregates raised.
    /// This is the modern replacement for database triggers: the same side effects, but
    /// in visible, testable, version-controlled application code.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregatesWithEvents = ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        var affectedRows = await base.SaveChangesAsync(cancellationToken);

        // Clear first so a handler that itself saves can't republish the same events.
        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        if (domainEvents.Count > 0)
        {
            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        return affectedRows;
    }
}
