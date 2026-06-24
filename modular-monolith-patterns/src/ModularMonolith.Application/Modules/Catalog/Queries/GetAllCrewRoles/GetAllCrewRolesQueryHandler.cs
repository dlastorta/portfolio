using AutoMapper;
using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Modules.Catalog.Queries.GetAllCrewRoles;

public sealed class GetAllCrewRolesQueryHandler(
    IUnitOfWork unitOfWork,
    IMapper mapper)
    : IQueryHandler<GetAllCrewRolesQuery, IReadOnlyList<CrewRoleDto>>
{
    public async Task<Result<IReadOnlyList<CrewRoleDto>>> Handle(GetAllCrewRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await unitOfWork.CrewRoles.GetAllAsync(cancellationToken);
        var dtos = mapper.Map<List<CrewRoleDto>>(roles);

        return Result.Success<IReadOnlyList<CrewRoleDto>>(dtos);
    }
}
