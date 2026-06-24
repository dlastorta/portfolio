using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModularMonolith.Application.Modules.Jobs.Commands.CreateJob;
using ModularMonolith.Domain.Modules.Jobs.Events;
using ModularMonolith.UnitTests.Common;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class CreateJobCommandHandlerTests
{
    [Fact]
    public async Task Handle_persists_the_job_and_dispatches_the_created_event()
    {
        using var harness = new ApplicationTestHarness();
        var handler = new CreateJobCommandHandler(
            harness.UnitOfWork,
            new FakeClock(DateTime.UtcNow),
            harness.Mapper,
            NullLogger<CreateJobCommandHandler>.Instance);

        var result = await handler.Handle(new CreateJobCommand("Pour foundation"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Pour foundation");
        result.Value.Status.Should().Be("Draft");

        harness.Dispatcher.Dispatched.OfType<JobCreatedEvent>().Should().ContainSingle();

        var persisted = await harness.UnitOfWork.Jobs.GetAllAsync();
        persisted.Should().ContainSingle(job => job.Title == "Pour foundation");
    }
}
