using System.Globalization;
using System.Text.Json;

namespace ErpSystem.Core.Services.Ehc.Sla;

public sealed class EhcSlaCalendarConfiguration
{
    public string? TimeZoneId { get; set; }
    public string? WorkdayStart { get; set; } // "HH:mm"
    public string? WorkdayEnd { get; set; } // "HH:mm"
    public int[]? WorkingDays { get; set; } // 0=Sunday..6=Saturday
    public string[]? Holidays { get; set; } // "yyyy-MM-dd" (local date)

    public static bool TryParse(string json, out EhcSlaCalendarConfiguration? config, out string error)
    {
        config = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "CalendarConfigurationJson is empty.";
            return false;
        }

        try
        {
            config = JsonSerializer.Deserialize<EhcSlaCalendarConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }

        if (config == null)
        {
            error = "Invalid calendar configuration.";
            return false;
        }

        return true;
    }

    public static bool TryParseAndNormalize(string json, out string normalizedJson, out string error)
    {
        normalizedJson = string.Empty;
        error = string.Empty;

        if (!TryParse(json, out var cfg, out error) || cfg == null)
        {
            return false;
        }

        cfg.TimeZoneId = string.IsNullOrWhiteSpace(cfg.TimeZoneId) ? null : cfg.TimeZoneId.Trim();

        if (!string.IsNullOrWhiteSpace(cfg.TimeZoneId))
        {
            if (!TryResolveTimeZone(cfg.TimeZoneId, out _, out var tzError))
            {
                error = tzError;
                return false;
            }
        }

        var workStart = ParseTime(cfg.WorkdayStart) ?? new TimeOnly(8, 0);
        var workEnd = ParseTime(cfg.WorkdayEnd) ?? new TimeOnly(17, 0);
        if (workEnd <= workStart)
        {
            error = "WorkdayEnd must be after WorkdayStart.";
            return false;
        }

        cfg.WorkdayStart = workStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        cfg.WorkdayEnd = workEnd.ToString("HH:mm", CultureInfo.InvariantCulture);

        var workingDays = cfg.WorkingDays == null || cfg.WorkingDays.Length == 0
            ? new[] { 1, 2, 3, 4, 5 }
            : cfg.WorkingDays.Where(d => d is >= 0 and <= 6).Distinct().OrderBy(x => x).ToArray();

        if (workingDays.Length == 0)
        {
            workingDays = new[] { 1, 2, 3, 4, 5 };
        }

        cfg.WorkingDays = workingDays;

        var holidays = new HashSet<DateOnly>();
        foreach (var h in cfg.Holidays ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(h)) continue;
            var text = h.Trim();
            if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                error = $"Invalid holiday date '{text}'. Expected yyyy-MM-dd.";
                return false;
            }
            holidays.Add(d);
        }

        cfg.Holidays = holidays.OrderBy(x => x).Select(x => x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToArray();

        normalizedJson = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = false });
        return true;
    }

    public static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo timeZone, out string error)
    {
        timeZone = TimeZoneInfo.Utc;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZone = TimeZoneInfo.Utc;
            return true;
        }

        var id = timeZoneId.Trim();

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch
        {
            // try IANA <-> Windows conversion (depending on OS)
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var winId))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(winId);
                    return true;
                }
                catch
                {
                    // ignore
                }
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
                    return true;
                }
                catch
                {
                    // ignore
                }
            }

            error = $"Unknown TimeZoneId '{id}'. Use a valid system time zone id (or omit to use UTC).";
            return false;
        }
    }

    public static TimeOnly? ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var text = s.Trim();
        if (TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t;
        if (TimeOnly.TryParseExact(text, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return t;
        if (TimeOnly.TryParseExact(text, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return t;

        return null;
    }
}

