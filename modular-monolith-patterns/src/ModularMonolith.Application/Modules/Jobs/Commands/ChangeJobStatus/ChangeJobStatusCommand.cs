using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Application.Modules.Jobs.Commands.ChangeJobStatus;

public sealed record ChangeJobStatusCommand(Guid JobId, JobStatus NewStatus) : ICommand<JobDto>;
