using Microsoft.EntityFrameworkCore;
using ModularMonolith.Domain.Abstractions;

namespace ModularMonolith.Infrastructure.Persistence;

public sealed class UnitOfWork(
    ApplicationDbContext context,
    IJobRepository jobs,
    ICrewRoleRepository crewRoles)
    : IUnitOfWork
{
    public IJobRepository Jobs => jobs;

    public ICrewRoleRepository CrewRoles => crewRoles;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // The execution strategy is what makes this safe under a retrying provider;
        // it's a no-op retry-wise on SQLite but keeps the pattern provider-agnostic.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
