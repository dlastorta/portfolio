using FluentAssertions;
using ModularMonolith.Application.Modules.Catalog.Queries.GetAllCrewRoles;
using ModularMonolith.Application.Modules.Jobs.Queries.GetJobById;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Catalog;
using ModularMonolith.Domain.Modules.Jobs;
using ModularMonolith.UnitTests.Common;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class JobQueryHandlerTests
{
    [Fact]
    public async Task GetJobById_returns_NotFound_when_missing()
    {
        using var harness = new ApplicationTestHarness();
        var handler = new GetJobByIdQueryHandler(harness.UnitOfWork, harness.Mapper);

        var result = await handler.Handle(new GetJobByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetJobById_returns_the_job_when_present()
    {
        using var harness = new ApplicationTestHarness();
        var job = Job.Create("Inspect site", DateTime.UtcNow);
        await harness.UnitOfWork.Jobs.AddAsync(job);
        await harness.UnitOfWork.SaveChangesAsync();

        var handler = new GetJobByIdQueryHandler(harness.UnitOfWork, harness.Mapper);
        var result = await handler.Handle(new GetJobByIdQuery(job.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Inspect site");
    }

    [Fact]
    public async Task GetAllCrewRoles_returns_seeded_roles()
    {
        using var harness = new ApplicationTestHarness();
        harness.Context.CrewRoles.AddRange(CrewRole.Create("Operator"), CrewRole.Create("Foreman"));
        await harness.Context.SaveChangesAsync();

        var handler = new GetAllCrewRolesQueryHandler(harness.UnitOfWork, harness.Mapper);
        var result = await handler.Handle(new GetAllCrewRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }
}
