using ModularMonolith.Domain.Common;

namespace ModularMonolith.Infrastructure.Time;

/// <summary>The production <see cref="IClock"/>: the real system clock, in UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
