namespace ModularMonolith.Domain.Abstractions;

/// <summary>
/// The unit of work. Exposes the repositories and the save/transaction boundary,
/// but deliberately leaks no persistence types (no <c>DbContext</c>, no
/// <c>IDbContextTransaction</c>) so the Domain stays free of EF Core.
/// </summary>
public interface IUnitOfWork
{
    IJobRepository Jobs { get; }

    ICrewRoleRepository CrewRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction, committing on
    /// success and rolling back if it throws. Used by the transaction pipeline behavior.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
