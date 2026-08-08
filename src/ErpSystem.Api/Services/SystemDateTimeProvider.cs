using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Api.Services;

/// <summary>
/// Production implementation of <see cref="IDateTimeProvider"/> that delegates
/// directly to the system clock.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
