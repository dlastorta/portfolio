using ModularMonolith.Application.Common.Messaging;

namespace ModularMonolith.Application.Modules.Jobs.Commands.CreateJob;

public sealed record CreateJobCommand(string Title) : ICommand<JobDto>;
