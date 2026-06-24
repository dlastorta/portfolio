using ModularMonolith.Domain.Common;

namespace ModularMonolith.UnitTests.Common;

public sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}
