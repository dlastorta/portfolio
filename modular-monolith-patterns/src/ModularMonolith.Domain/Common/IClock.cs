namespace ModularMonolith.Domain.Common;

/// <summary>
/// Abstraction over the system clock. The interface lives in the Domain; the
/// concrete implementation lives in Infrastructure. This keeps time testable
/// (no <c>DateTime.UtcNow</c> scattered through business logic) and is a small,
/// classic example of an infrastructure concern expressed as a Domain port.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
