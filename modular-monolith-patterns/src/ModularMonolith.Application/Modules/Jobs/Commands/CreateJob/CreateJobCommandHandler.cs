using Microsoft.Extensions.Logging;
using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Application.Modules.Jobs.Commands.CreateJob;

public sealed class CreateJobCommandHandler(
    IUnitOfWork unitOfWork,
    IClock clock,
    JobMapper mapper,
    ILogger<CreateJobCommandHandler> logger)
    : ICommandHandler<CreateJobCommand, JobDto>
{
    public async Task<Result<JobDto>> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        var job = Job.Create(request.Title, clock.UtcNow);

        await unitOfWork.Jobs.AddAsync(job, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created job {JobId}", job.Id);

        return Result.Success(mapper.ToDto(job));
    }
}
