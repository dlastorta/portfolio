using MediatR;
using ModularMonolith.Application.Modules.Jobs.Commands.ChangeJobStatus;
using ModularMonolith.Application.Modules.Jobs.Commands.CreateJob;
using ModularMonolith.Application.Modules.Jobs.Queries.GetAllJobs;
using ModularMonolith.Application.Modules.Jobs.Queries.GetJobById;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.WebApi.Endpoints;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/jobs").WithTags("Jobs");

        group.MapPost("/", async (CreateJobRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateJobCommand(request.Title), ct);
            return result.ToHttpResult(dto => Results.Created($"/jobs/{dto.Id}", dto));
        });

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetAllJobsQuery(), ct)).ToHttpResult());

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetJobByIdQuery(id), ct)).ToHttpResult());

        group.MapPut("/{id:guid}/status", async (Guid id, ChangeJobStatusRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ChangeJobStatusCommand(id, request.NewStatus), ct)).ToHttpResult());

        return app;
    }
}

public sealed record CreateJobRequest(string Title);

public sealed record ChangeJobStatusRequest(JobStatus NewStatus);
