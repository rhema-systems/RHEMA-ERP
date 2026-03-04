namespace ErpSystem.Core.Services.Ehc.Sla;

using System.Globalization;

public static class EhcSlaTimeCalculator
{
    public static DateTime CalculateDueAtUtc(DateTime startUtc, int minutes, string? calendarConfigurationJson)
    {
        if (minutes <= 0) return startUtc;

        if (string.IsNullOrWhiteSpace(calendarConfigurationJson))
        {
            return startUtc.AddMinutes(minutes);
        }

        if (!EhcSlaCalendarConfiguration.TryParse(calendarConfigurationJson, out var cfg, out _))
        {
            return startUtc.AddMinutes(minutes);
        }

        var tz = ResolveTimeZone(cfg?.TimeZoneId);

        var workStart = EhcSlaCalendarConfiguration.ParseTime(cfg?.WorkdayStart) ?? new TimeOnly(8, 0);
        var workEnd = EhcSlaCalendarConfiguration.ParseTime(cfg?.WorkdayEnd) ?? new TimeOnly(17, 0);
        if (workEnd <= workStart)
        {
            return startUtc.AddMinutes(minutes);
        }

        var workingDays = BuildWorkingDays(cfg?.WorkingDays);
        var holidays = BuildHolidays(cfg?.Holidays);

        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), tz);
        var dueLocal = AddBusinessMinutes(startLocal, minutes, workStart, workEnd, workingDays, holidays);
        return ConvertLocalToUtcSafe(dueLocal, tz);
    }

    public static int CalculateBusinessMinutesBetweenUtc(DateTime startUtc, DateTime endUtc, string? calendarConfigurationJson)
    {
        if (endUtc <= startUtc) return 0;

        if (string.IsNullOrWhiteSpace(calendarConfigurationJson))
        {
            return (int)Math.Max(0, Math.Ceiling((endUtc - startUtc).TotalMinutes));
        }

        if (!EhcSlaCalendarConfiguration.TryParse(calendarConfigurationJson, out var cfg, out _))
        {
            return (int)Math.Max(0, Math.Ceiling((endUtc - startUtc).TotalMinutes));
        }

        var tz = ResolveTimeZone(cfg?.TimeZoneId);

        var workStart = EhcSlaCalendarConfiguration.ParseTime(cfg?.WorkdayStart) ?? new TimeOnly(8, 0);
        var workEnd = EhcSlaCalendarConfiguration.ParseTime(cfg?.WorkdayEnd) ?? new TimeOnly(17, 0);
        if (workEnd <= workStart)
        {
            return (int)Math.Max(0, Math.Ceiling((endUtc - startUtc).TotalMinutes));
        }

        var workingDays = BuildWorkingDays(cfg?.WorkingDays);
        var holidays = BuildHolidays(cfg?.Holidays);

        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), tz);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), tz);

        if (endLocal <= startLocal) return 0;

        var totalMinutes = 0.0;
        var day = DateOnly.FromDateTime(startLocal);
        var endDay = DateOnly.FromDateTime(endLocal);

        while (day <= endDay)
        {
            if (!workingDays.Contains(day.DayOfWeek) || holidays.Contains(day))
            {
                day = day.AddDays(1);
                continue;
            }

            var intervalStart = day.ToDateTime(workStart);
            var intervalEnd = day.ToDateTime(workEnd);

            var from = startLocal > intervalStart ? startLocal : intervalStart;
            var to = endLocal < intervalEnd ? endLocal : intervalEnd;

            if (to > from)
            {
                totalMinutes += (to - from).TotalMinutes;
            }

            day = day.AddDays(1);
        }

        return (int)Math.Max(0, Math.Ceiling(totalMinutes));
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId.Trim(), out var winId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(winId); } catch { /* ignore */ }
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId.Trim(), out var ianaId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); } catch { /* ignore */ }
            }

            return TimeZoneInfo.Utc;
        }
    }

    private static HashSet<DayOfWeek> BuildWorkingDays(int[]? workingDays)
    {
        var set = new HashSet<DayOfWeek>();
        if (workingDays == null || workingDays.Length == 0)
        {
            set.Add(DayOfWeek.Monday);
            set.Add(DayOfWeek.Tuesday);
            set.Add(DayOfWeek.Wednesday);
            set.Add(DayOfWeek.Thursday);
            set.Add(DayOfWeek.Friday);
            return set;
        }

        foreach (var d in workingDays)
        {
            if (d < 0 || d > 6) continue;
            set.Add((DayOfWeek)d);
        }

        if (set.Count == 0)
        {
            set.Add(DayOfWeek.Monday);
            set.Add(DayOfWeek.Tuesday);
            set.Add(DayOfWeek.Wednesday);
            set.Add(DayOfWeek.Thursday);
            set.Add(DayOfWeek.Friday);
        }

        return set;
    }

    private static HashSet<DateOnly> BuildHolidays(string[]? holidays)
    {
        var set = new HashSet<DateOnly>();
        if (holidays == null || holidays.Length == 0) return set;

        foreach (var h in holidays)
        {
            if (string.IsNullOrWhiteSpace(h)) continue;
            var text = h.Trim();
            if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                set.Add(d);
            }
        }

        return set;
    }

    private static DateTime AddBusinessMinutes(
        DateTime startLocal,
        int minutes,
        TimeOnly workStart,
        TimeOnly workEnd,
        HashSet<DayOfWeek> workingDays,
        HashSet<DateOnly> holidays)
    {
        var remaining = TimeSpan.FromMinutes(minutes);
        var cursor = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);

        while (remaining > TimeSpan.Zero)
        {
            var localDate = DateOnly.FromDateTime(cursor);

            if (!workingDays.Contains(cursor.DayOfWeek) || holidays.Contains(localDate))
            {
                cursor = localDate.AddDays(1).ToDateTime(workStart);
                continue;
            }

            var intervalStart = localDate.ToDateTime(workStart);
            var intervalEnd = localDate.ToDateTime(workEnd);

            if (cursor < intervalStart)
            {
                cursor = intervalStart;
            }

            if (cursor >= intervalEnd)
            {
                cursor = localDate.AddDays(1).ToDateTime(workStart);
                continue;
            }

            var available = intervalEnd - cursor;
            if (available >= remaining)
            {
                return cursor.Add(remaining);
            }

            remaining -= available;
            cursor = intervalEnd;
        }

        return cursor;
    }

    private static DateTime ConvertLocalToUtcSafe(DateTime localUnspecified, TimeZoneInfo tz)
    {
        var local = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);

        if (tz.IsInvalidTime(local))
        {
            // Shift forward into the next valid time window (DST gap).
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }
}
