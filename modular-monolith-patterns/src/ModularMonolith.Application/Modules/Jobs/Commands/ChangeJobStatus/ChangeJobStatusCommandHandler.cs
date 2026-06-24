using AutoMapper;
using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Modules.Jobs.Commands.ChangeJobStatus;

public sealed class ChangeJobStatusCommandHandler(
    IUnitOfWork unitOfWork,
    IClock clock,
    IMapper mapper)
    : ICommandHandler<ChangeJobStatusCommand, JobDto>
{
    public async Task<Result<JobDto>> Handle(ChangeJobStatusCommand request, CancellationToken cancellationToken)
    {
        // Tracking load: we intend to mutate and persist this aggregate.
        var job = await unitOfWork.Jobs.GetByIdAsync(request.JobId, asNoTracking: false, cancellationToken);

        if (job is null)
        {
            return Result.Failure<JobDto>(Error.NotFound($"Job '{request.JobId}' was not found."));
        }

        var transition = job.ChangeStatus(request.NewStatus, clock.UtcNow);
        if (transition.IsFailure)
        {
            // The domain rejected the transition; surface its error unchanged.
            return Result.Failure<JobDto>(transition.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(mapper.Map<JobDto>(job));
    }
}
