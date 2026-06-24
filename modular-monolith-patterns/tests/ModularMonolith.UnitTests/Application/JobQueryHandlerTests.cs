using FluentAssertions;
using ModularMonolith.Application.Modules.Catalog;
using ModularMonolith.Application.Modules.Catalog.Queries.GetAllCrewRoles;
using ModularMonolith.Application.Modules.Jobs;
using ModularMonolith.Application.Modules.Jobs.Queries.GetJobById;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Catalog;
using ModularMonolith.Domain.Modules.Jobs;
using NSubstitute;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class JobQueryHandlerTests
{
    [Fact]
    public async Task GetJobById_returns_NotFound_when_missing()
    {
        var jobs = Substitute.For<IJobRepository>();
        jobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(null));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Jobs.Returns(jobs);

        var handler = new GetJobByIdQueryHandler(unitOfWork, new JobMapper());
        var result = await handler.Handle(new GetJobByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetJobById_returns_the_mapped_job_when_present()
    {
        var job = Job.Create("Inspect site", DateTime.UtcNow);
        var jobs = Substitute.For<IJobRepository>();
        jobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Jobs.Returns(jobs);

        var handler = new GetJobByIdQueryHandler(unitOfWork, new JobMapper());
        var result = await handler.Handle(new GetJobByIdQuery(job.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Inspect site");
        result.Value.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetAllCrewRoles_returns_the_mapped_roles()
    {
        IReadOnlyList<CrewRole> roles = [CrewRole.Create("Operator"), CrewRole.Create("Foreman")];
        var crewRoles = Substitute.For<ICrewRoleRepository>();
        crewRoles.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(roles));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.CrewRoles.Returns(crewRoles);

        var handler = new GetAllCrewRolesQueryHandler(unitOfWork, new CrewRoleMapper());
        var result = await handler.Handle(new GetAllCrewRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(r => r.Name).Should().BeEquivalentTo(new[] { "Operator", "Foreman" });
    }
}
