using AutoMapper;
using ModularMonolith.Domain.Modules.Catalog;

namespace ModularMonolith.Application.Modules.Catalog;

public sealed class CrewRoleMappingProfile : Profile
{
    public CrewRoleMappingProfile()
    {
        CreateMap<CrewRole, CrewRoleDto>();
    }
}
