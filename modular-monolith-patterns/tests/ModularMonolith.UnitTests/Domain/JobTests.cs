using FluentAssertions;
using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Jobs;
using ModularMonolith.Domain.Modules.Jobs.Events;
using Xunit;

namespace ModularMonolith.UnitTests.Domain;

public class JobTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_starts_in_draft_and_raises_created_event()
    {
        var job = Job.Create("Pour foundation", Now);

        job.Title.Should().Be("Pour foundation");
        job.Status.Should().Be(JobStatus.Draft);
        job.DomainEvents.OfType<JobCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_rejects_blank_title()
    {
        var act = () => Job.Create("   ", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeStatus_allows_a_valid_transition_and_raises_event()
    {
        var job = Job.Create("x", Now);

        var result = job.ChangeStatus(JobStatus.Scheduled, Now);

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Scheduled);
        job.DomainEvents.OfType<JobStatusChangedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void ChangeStatus_rejects_an_invalid_transition()
    {
        var job = Job.Create("x", Now); // Draft

        var result = job.ChangeStatus(JobStatus.Completed, Now); // not reachable from Draft

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        job.Status.Should().Be(JobStatus.Draft);
    }

    [Fact]
    public void ChangeStatus_to_the_same_status_fails()
    {
        var job = Job.Create("x", Now);

        var result = job.ChangeStatus(JobStatus.Draft, Now);

        result.IsFailure.Should().BeTrue();
    }
}
