using FluentAssertions;
using ModularMonolith.Application.Modules.Jobs;
using ModularMonolith.Application.Modules.Jobs.Commands.ChangeJobStatus;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Jobs;
using ModularMonolith.UnitTests.Common;
using NSubstitute;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class ChangeJobStatusCommandHandlerTests
{
    private static (IUnitOfWork UnitOfWork, IJobRepository Jobs) CreateUnitOfWork()
    {
        var jobs = Substitute.For<IJobRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Jobs.Returns(jobs);
        return (unitOfWork, jobs);
    }

    private static ChangeJobStatusCommandHandler CreateHandler(IUnitOfWork unitOfWork) =>
        new(unitOfWork, new FakeClock(DateTime.UtcNow), new JobMapper());

    [Fact]
    public async Task Handle_returns_NotFound_and_does_not_save_when_the_job_is_missing()
    {
        var (unitOfWork, jobs) = CreateUnitOfWork();
        jobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(null));

        var result = await CreateHandler(unitOfWork).Handle(
            new ChangeJobStatusCommand(Guid.NewGuid(), JobStatus.Scheduled),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_changes_status_and_saves_on_a_valid_transition()
    {
        var (unitOfWork, jobs) = CreateUnitOfWork();
        var job = Job.Create("x", DateTime.UtcNow); // Draft
        jobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        var result = await CreateHandler(unitOfWork).Handle(
            new ChangeJobStatusCommand(job.Id, JobStatus.Scheduled),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Scheduled");
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_Conflict_and_does_not_save_on_an_invalid_transition()
    {
        var (unitOfWork, jobs) = CreateUnitOfWork();
        var job = Job.Create("x", DateTime.UtcNow); // Draft -> Completed is not allowed
        jobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        var result = await CreateHandler(unitOfWork).Handle(
            new ChangeJobStatusCommand(job.Id, JobStatus.Completed),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
