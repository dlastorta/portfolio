using ModularMonolith.Application.Common.Messaging;

namespace ModularMonolith.Application.Modules.Jobs.Queries.GetJobById;

public sealed record GetJobByIdQuery(Guid JobId) : IQuery<JobDto>;
