using Microsoft.EntityFrameworkCore;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Modules.Catalog;

namespace ModularMonolith.Infrastructure.Persistence.Repositories;

public sealed class CrewRoleRepository(ApplicationDbContext context) : ICrewRoleRepository
{
    public async Task<IReadOnlyList<CrewRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.CrewRoles
            .AsNoTracking()
            .OrderBy(crewRole => crewRole.Name)
            .ToListAsync(cancellationToken);
    }
}
