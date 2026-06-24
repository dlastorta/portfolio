using ModularMonolith.Domain.Common;
using ModularMonolith.Domain.Modules.Jobs.Events;

namespace ModularMonolith.Domain.Modules.Jobs;

/// <summary>
/// The Job aggregate. State changes go through methods that enforce the rules and
/// raise domain events — callers can't just set <see cref="Status"/> directly.
/// </summary>
public sealed class Job : AggregateRoot
{
    // Required by EF Core's materializer.
    private Job()
    {
    }

    private Job(Guid id, string title, JobStatus status, DateTime createdAtUtc)
    {
        Id = id;
        Title = title;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public string Title { get; private set; } = string.Empty;

    public JobStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Job Create(string title, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        var job = new Job(Guid.NewGuid(), title.Trim(), JobStatus.Draft, utcNow);
        job.Raise(new JobCreatedEvent(job.Id, job.Title, utcNow));
        return job;
    }

    /// <summary>
    /// Moves the job to a new status if the transition is allowed. Returns a failed
    /// <see cref="Result"/> rather than throwing for an invalid transition, because
    /// "you can't do that from here" is a normal business outcome, not a bug.
    /// </summary>
    public Result ChangeStatus(JobStatus newStatus, DateTime utcNow)
    {
        if (newStatus == Status)
        {
            return Result.Failure(Error.Conflict($"Job is already in status '{Status}'."));
        }

        if (!IsTransitionAllowed(Status, newStatus))
        {
            return Result.Failure(Error.Conflict($"Cannot move a job from '{Status}' to '{newStatus}'."));
        }

        var previous = Status;
        Status = newStatus;
        Raise(new JobStatusChangedEvent(Id, previous, newStatus, utcNow));
        return Result.Success();
    }

    private static bool IsTransitionAllowed(JobStatus from, JobStatus to) => from switch
    {
        JobStatus.Draft => to is JobStatus.Scheduled or JobStatus.Cancelled,
        JobStatus.Scheduled => to is JobStatus.Active or JobStatus.Cancelled,
        JobStatus.Active => to is JobStatus.Completed or JobStatus.Cancelled,
        JobStatus.Completed => false,
        JobStatus.Cancelled => false,
        _ => false
    };
}
