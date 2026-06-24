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
        // Going through the execution strategy is what makes this correct under a
        // retrying provider (e.g. SQL Server with EnableRetryOnFailure): the whole
        // transaction is replayed as a unit on a transient failure.
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
