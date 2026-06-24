using FluentAssertions;
using ModularMonolith.Application.Modules.Jobs.Commands.ChangeJobStatus;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Jobs;
using ModularMonolith.Domain.Modules.Jobs.Events;
using ModularMonolith.UnitTests.Common;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class ChangeJobStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_returns_NotFound_when_the_job_does_not_exist()
    {
        using var harness = new ApplicationTestHarness();
        var handler = new ChangeJobStatusCommandHandler(
            harness.UnitOfWork,
            new FakeClock(DateTime.UtcNow),
            harness.Mapper);

        var result = await handler.Handle(
            new ChangeJobStatusCommand(Guid.NewGuid(), JobStatus.Scheduled),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_changes_status_and_dispatches_the_event()
    {
        using var harness = new ApplicationTestHarness();
        var job = Job.Create("x", DateTime.UtcNow);
        await harness.UnitOfWork.Jobs.AddAsync(job);
        await harness.UnitOfWork.SaveChangesAsync();
        harness.Dispatcher.Dispatched.Clear(); // ignore the JobCreated event from setup

        var handler = new ChangeJobStatusCommandHandler(
            harness.UnitOfWork,
            new FakeClock(DateTime.UtcNow),
            harness.Mapper);

        var result = await handler.Handle(
            new ChangeJobStatusCommand(job.Id, JobStatus.Scheduled),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Scheduled");
        harness.Dispatcher.Dispatched.OfType<JobStatusChangedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_returns_Conflict_for_an_invalid_transition()
    {
        using var harness = new ApplicationTestHarness();
        var job = Job.Create("x", DateTime.UtcNow);
        await harness.UnitOfWork.Jobs.AddAsync(job);
        await harness.UnitOfWork.SaveChangesAsync();

        var handler = new ChangeJobStatusCommandHandler(
            harness.UnitOfWork,
            new FakeClock(DateTime.UtcNow),
            harness.Mapper);

        // Draft -> Completed is not an allowed transition.
        var result = await handler.Handle(
            new ChangeJobStatusCommand(job.Id, JobStatus.Completed),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }
}
