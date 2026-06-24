using Microsoft.EntityFrameworkCore;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Infrastructure.Persistence.Repositories;

public sealed class JobRepository(ApplicationDbContext context) : IJobRepository
{
    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        await context.Jobs.AddAsync(job, cancellationToken);
    }

    public async Task<Job?> GetByIdAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Job> query = context.Jobs;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Jobs
            .AsNoTracking()
            .OrderByDescending(job => job.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
