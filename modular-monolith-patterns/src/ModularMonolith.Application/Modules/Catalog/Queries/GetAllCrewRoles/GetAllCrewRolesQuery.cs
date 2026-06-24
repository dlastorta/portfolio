using ModularMonolith.Application.Common.Messaging;

namespace ModularMonolith.Application.Modules.Catalog.Queries.GetAllCrewRoles;

public sealed record GetAllCrewRolesQuery : IQuery<IReadOnlyList<CrewRoleDto>>;
