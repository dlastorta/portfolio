using Microsoft.EntityFrameworkCore;
using ModularMonolith.Domain.Modules.Catalog;

namespace ModularMonolith.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    /// <summary>
    /// Creates the database (demo convenience — a real deployment uses migrations) and
    /// seeds the Catalog module's reference data if it's empty.
    /// </summary>
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (await context.CrewRoles.AnyAsync(cancellationToken))
        {
            return;
        }

        context.CrewRoles.AddRange(
            CrewRole.Create("Operator"),
            CrewRole.Create("Foreman"),
            CrewRole.Create("Laborer"),
            CrewRole.Create("Mechanic"));

        await context.SaveChangesAsync(cancellationToken);
    }
}
