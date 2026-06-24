using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModularMonolith.Application.Modules.Jobs;
using ModularMonolith.Application.Modules.Jobs.Commands.CreateJob;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Modules.Jobs;
using ModularMonolith.Domain.Modules.Jobs.Events;
using ModularMonolith.UnitTests.Common;
using NSubstitute;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class CreateJobCommandHandlerTests
{
    [Fact]
    public async Task Handle_creates_a_draft_job_persists_it_and_returns_the_dto()
    {
        var jobs = Substitute.For<IJobRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Jobs.Returns(jobs);

        var handler = new CreateJobCommandHandler(
            unitOfWork,
            new FakeClock(DateTime.UtcNow),
            new JobMapper(),
            NullLogger<CreateJobCommandHandler>.Instance);

        var result = await handler.Handle(new CreateJobCommand("Pour foundation"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Pour foundation");
        result.Value.Status.Should().Be("Draft");

        // The aggregate was added to the repository, carrying its JobCreated event,
        // and the unit of work was saved exactly once.
        await jobs.Received(1).AddAsync(
            Arg.Is<Job>(job => job.Title == "Pour foundation"
                               && job.DomainEvents.OfType<JobCreatedEvent>().Any()),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
