using ModularMonolith.Domain.Modules.Catalog;
using Riok.Mapperly.Abstractions;

namespace ModularMonolith.Application.Modules.Catalog;

[Mapper]
public partial class CrewRoleMapper
{
    public partial CrewRoleDto ToDto(CrewRole crewRole);

    public partial List<CrewRoleDto> ToDtos(IEnumerable<CrewRole> crewRoles);
}
