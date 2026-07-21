namespace ErpSystem.Core.Interfaces.Common;

/// <summary>
/// Abstraction over the system clock, enabling deterministic unit testing.
/// Inject this wherever "today" or "now" is needed instead of DateTime.UtcNow directly.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Current UTC date and time.</summary>
    DateTime UtcNow { get; }

    /// <summary>Current UTC date as a <see cref="DateOnly"/> — used for due-date comparisons.</summary>
    DateOnly TodayUtc { get; }
}
