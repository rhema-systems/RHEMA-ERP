using ErpSystem.Core.Entities.HR.StaffLeave;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Which dates a stretch of leave costs, given the holidays inside it — the day walk itself.
/// </summary>
/// <remarks>
/// <para>Moved out of <c>LeaveService</c> unchanged in round 5, lane G, because more readers now need
/// to count leave exactly as it was charged: the year-end expiry run, reminder sweep 5 and the
/// "leave owed" report all ask how much of a request fell on or before a date. A second walk over
/// the same rules would drift the first time either side was edited — the reason the charge and the
/// attendance posting already share one.</para>
///
/// <para>The caller supplies the holidays (<c>IHrWorkingDayCalculator.GetHolidayDatesAsync</c>), so
/// this stays pure: a set loaded for a wider range gives the same answer, since only the dates
/// inside the stretch are looked at.</para>
/// </remarks>
public static class LeaveChargeableDays
{
    /// <summary>The chargeable dates from <paramref name="start"/> to <paramref name="end"/>, in order.</summary>
    public static List<DateOnly> Between(
        DateOnly start, DateOnly end, LeaveType leaveType, IReadOnlySet<DateOnly> holidays)
    {
        var days = new List<DateOnly>();
        for (var currentDate = start; currentDate <= end; currentDate = currentDate.AddDays(1))
        {
            bool isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;
            bool isHoliday = holidays.Contains(currentDate);

            if (!leaveType.CountWeekendsAsLeave && isWeekend) continue;
            if (!leaveType.CountHolidaysAsLeave && isHoliday) continue;

            days.Add(currentDate);
        }

        return days;
    }
}
