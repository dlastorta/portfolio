using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Modules.Jobs.Queries.GetAllJobs;

public sealed class GetAllJobsQueryHandler(
    IUnitOfWork unitOfWork,
    JobMapper mapper)
    : IQueryHandler<GetAllJobsQuery, IReadOnlyList<JobDto>>
{
    public async Task<Result<IReadOnlyList<JobDto>>> Handle(GetAllJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await unitOfWork.Jobs.GetAllAsync(cancellationToken);
        var dtos = mapper.ToDtos(jobs);

        return Result.Success<IReadOnlyList<JobDto>>(dtos);
    }
}
