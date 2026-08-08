using ErpSystem.Core.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// Finance-facing adapter over the currently maintained Payroll holiday setup.
/// Keep consumers behind IBusinessCalendarProvider so this can be realigned with
/// a future consolidated HR/Payroll calendar without changing recurrence logic.
/// </summary>
public sealed class PayrollBusinessCalendarProvider : IBusinessCalendarProvider
{
    private readonly ApplicationDbContext _db;

    public PayrollBusinessCalendarProvider(ApplicationDbContext db) => _db = db;

    public async Task<bool> IsBusinessDayAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;

        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        // Recurring-journal generation also runs from a background worker where no
        // request-scoped tenant exists. Ignore the ambient query filter only after
        // applying the explicit tenant predicate so one tenant's holiday cannot
        // affect another tenant's accounting schedule.
        return !await _db.PayrollHolidays.IgnoreQueryFilters().AsNoTracking().AnyAsync(h =>
            h.TenantId == tenantId && !h.IsDeleted && h.HolidayDate >= start && h.HolidayDate < end,
            cancellationToken);
    }
}
