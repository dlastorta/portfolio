using ModularMonolith.Application.Common.Messaging;

namespace ModularMonolith.Application.Modules.Jobs.Queries.GetAllJobs;

public sealed record GetAllJobsQuery : IQuery<IReadOnlyList<JobDto>>;
