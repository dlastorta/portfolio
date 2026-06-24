using AutoMapper;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Application.Modules.Jobs;

public sealed class JobMappingProfile : Profile
{
    public JobMappingProfile()
    {
        CreateMap<Job, JobDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
    }
}
