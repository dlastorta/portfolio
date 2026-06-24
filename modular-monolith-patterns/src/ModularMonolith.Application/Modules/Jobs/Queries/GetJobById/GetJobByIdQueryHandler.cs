using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Modules.Jobs.Queries.GetJobById;

public sealed class GetJobByIdQueryHandler(
    IUnitOfWork unitOfWork,
    JobMapper mapper)
    : IQueryHandler<GetJobByIdQuery, JobDto>
{
    public async Task<Result<JobDto>> Handle(GetJobByIdQuery request, CancellationToken cancellationToken)
    {
        // No-tracking read: this is a query, nothing here will be persisted.
        var job = await unitOfWork.Jobs.GetByIdAsync(request.JobId, asNoTracking: true, cancellationToken);

        return job is null
            ? Result.Failure<JobDto>(Error.NotFound($"Job '{request.JobId}' was not found."))
            : Result.Success(mapper.ToDto(job));
    }
}
