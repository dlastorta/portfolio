namespace ModularMonolith.Domain.Modules.Jobs;

public enum JobStatus
{
    Draft = 0,
    Scheduled = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}
