namespace ModularMonolith.Application.Modules.Jobs;

public sealed class JobDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}
