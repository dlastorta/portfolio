using ModularMonolith.Domain.Modules.Catalog;

namespace ModularMonolith.Domain.Abstractions;

/// <summary>Port for CrewRole reference data. Implemented in Infrastructure.</summary>
public interface ICrewRoleRepository
{
    Task<IReadOnlyList<CrewRole>> GetAllAsync(CancellationToken cancellationToken = default);
}
