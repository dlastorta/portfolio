using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Domain.Abstractions;

/// <summary>Port for Job persistence. Implemented in Infrastructure.</summary>
public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    Task<Job?> GetByIdAsync(Guid id, bool asNoTracking = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default);
}
