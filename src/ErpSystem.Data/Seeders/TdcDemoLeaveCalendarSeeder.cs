using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the leave vocabulary and the Ghanaian statutory holiday calendar for the <b>DEFAULT
/// tenant</b>.
///
/// <para><b>Why this exists.</b> A fresh database has zero leave types and zero public holidays.
/// The consequence is not a sparse screen but a dead module: the leave-request form opens with an
/// empty type dropdown, so no request can be raised, so no approval, balance, plan, encashment or
/// year-end screen has anything to show. Leave is the single most recognisable thing in an HR
/// system and it was the one thing that could not be demonstrated at all.</para>
///
/// <para><b>Two different kinds of fact, deliberately in one seeder.</b> The holidays are statutory
/// and verifiable; the leave entitlements are policy and are the client's to set. They ship together
/// because leave day-counting is meaningless without a calendar to exclude — seeding one without the
/// other produces a leave module that answers arithmetic questions wrongly, which is worse than one
/// that cannot answer them.</para>
///
/// <para>⚠ <b>The entitlements are a defensible starting point, not the client's policy.</b> They
/// follow the Ghana Labour Act 651 statutory floors where one exists (annual leave 15 working days,
/// maternity 12 weeks) and reasonable practice where none does. The real figures come from the HR
/// questionnaire. Anything seeded here is editable in the leave-type screen, which is itself worth
/// showing.</para>
///
/// <para>Idempotent per area: leave types and the calendar are guarded separately, so a run that
/// added the calendar and then failed can be repeated without duplicating the types.</para>
/// </summary>
public class TdcDemoLeaveCalendarSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoLeaveCalendarSeeder> _logger;

    private const string By = "TdcDemoLeaveCalendarSeeder";
    private const string CalendarName = "Ghana Statutory Holidays";

    public TdcDemoLeaveCalendarSeeder(ApplicationDbContext context, ILogger<TdcDemoLeaveCalendarSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed leave types or the holiday calendar.");
            return;
        }

        var tenantId = tenant.Id;

        await SeedLeaveTypesAsync(tenantId, ct);
        await SeedHolidayCalendarAsync(tenantId, ct);

        await _context.SaveChangesAsync(ct);
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Leave types
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private async Task SeedLeaveTypesAsync(Guid tenantId, CancellationToken ct)
    {
        if (await _context.Set<LeaveType>().IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct))
        {
            _logger.LogInformation("Leave types already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var types = new List<LeaveType>
        {
            new()
            {
                Name = "Annual Leave",
                Code = "ANN",
                Description = "Paid annual leave. The Labour Act 651 floor is 15 working days after "
                            + "12 months of continuous service; this policy grants more.",
                IsPaid = true,
                DefaultDaysPerYear = 21,
                MaxDaysPerYear = 30,
                MinDaysNotice = 14,
                RequiresApproval = true,
                RequiresReliever = true,
                CalendarColor = "#2E7D32",

                // Weekends and public holidays do not consume annual leave. This is the pairing the
                // holiday calendar below exists to serve.
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,

                AllowCarryOver = true,
                MaxCarryOverDays = 5,
                CarryOverExpiryMonths = 3,

                // The statutory access rule: annual leave is earned by service.
                MinServiceMonthsToAccess = 12,

                MandatoryAnnualLeave = true,
                ForfeitUnusedAfterMonths = 15,
                AllowCashConversion = true
            },
            new()
            {
                Name = "Sick Leave",
                Code = "SICK",
                Description = "Paid absence on medical grounds. A medical certificate is required "
                            + "beyond three consecutive days.",
                IsPaid = true,
                DefaultDaysPerYear = 12,
                MaxDaysPerYear = 20,
                MinDaysNotice = 0,
                RequiresApproval = true,
                CalendarColor = "#C62828",
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,
                AllowCarryOver = false,

                // Available from the first day. Nobody schedules illness around a service threshold.
                MinServiceMonthsToAccess = 0
            },
            new()
            {
                Name = "Maternity Leave",
                Code = "MAT",
                Description = "Paid maternity leave. The Labour Act 651 grants not less than twelve "
                            + "weeks, extended for a caesarean or multiple birth.",
                IsPaid = true,
                DefaultDaysPerYear = 84,
                MaxDaysPerYear = 98,
                MinDaysNotice = 30,
                RequiresApproval = true,
                CalendarColor = "#AD1457",

                // Statutory maternity leave runs in calendar days, not working days — the one type
                // here that deliberately counts weekends and holidays.
                CountWeekendsAsLeave = true,
                CountHolidaysAsLeave = true,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 0
            },
            new()
            {
                Name = "Paternity Leave",
                Code = "PAT",
                Description = "Paid leave for a father on the birth of a child. Policy, not statute.",
                IsPaid = true,
                DefaultDaysPerYear = 5,
                MaxDaysPerYear = 5,
                MinDaysNotice = 7,
                RequiresApproval = true,
                CalendarColor = "#1565C0",
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 0
            },
            new()
            {
                Name = "Compassionate Leave",
                Code = "COMP",
                Description = "Paid leave on the death or serious illness of an immediate relative.",
                IsPaid = true,
                DefaultDaysPerYear = 7,
                MaxDaysPerYear = 10,
                MinDaysNotice = 0,
                RequiresApproval = true,
                CalendarColor = "#4E342E",
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 0
            },
            new()
            {
                Name = "Casual Leave",
                Code = "CAS",
                Description = "Short paid absence for personal matters, taken in single days.",
                IsPaid = true,
                DefaultDaysPerYear = 6,
                MaxDaysPerYear = 6,
                MinDaysNotice = 2,
                RequiresApproval = true,
                CalendarColor = "#F9A825",
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 3
            },
            new()
            {
                Name = "Study Leave",
                Code = "STUDY",
                Description = "Paid leave to sit approved examinations or attend an approved "
                            + "programme of study. Normally tied to a service bond.",
                IsPaid = true,
                DefaultDaysPerYear = 10,
                MaxDaysPerYear = 30,
                MinDaysNotice = 30,
                RequiresApproval = true,
                CalendarColor = "#6A1B9A",
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 24
            },
            new()
            {
                Name = "Leave of Absence (Unpaid)",
                Code = "UNPAID",
                Description = "Approved absence without pay, where no paid entitlement applies.",
                IsPaid = false,
                DefaultDaysPerYear = 0,
                MaxDaysPerYear = 90,
                MinDaysNotice = 30,
                RequiresApproval = true,
                CalendarColor = "#616161",
                CountWeekendsAsLeave = true,
                CountHolidaysAsLeave = true,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 12
            },
            new()
            {
                Name = "Occupational Injury Leave",
                Code = "INJ",
                Description = "Paid absence arising from an injury sustained at work. Raised from, "
                            + "and evidenced by, the SHE incident record.",
                IsPaid = true,
                DefaultDaysPerYear = 0,
                MaxDaysPerYear = 180,
                MinDaysNotice = 0,
                RequiresApproval = true,
                CalendarColor = "#EF6C00",
                CountWeekendsAsLeave = false,
                CountHolidaysAsLeave = false,
                AllowCarryOver = false,
                MinServiceMonthsToAccess = 0
            }
        };

        foreach (var type in types)
        {
            type.Id = Guid.NewGuid();
            type.TenantId = tenantId;
            type.IsActive = true;
            type.CreatedAt = now;
            type.CreatedBy = By;
            _context.Set<LeaveType>().Add(type);
        }

        _logger.LogInformation(
            "Seeded {Count} leave types. These are a defensible starting point, NOT the client's "
            + "policy — replace the entitlements from the HR questionnaire.", types.Count);
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Holiday calendar
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private async Task SeedHolidayCalendarAsync(Guid tenantId, CancellationToken ct)
    {
        var calendar = await _context.Set<HolidayCalendar>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted
                                   && c.CalendarName == CalendarName, ct);

        var now = DateTime.UtcNow;

        if (calendar is null)
        {
            var ghana = await _context.Set<Country>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                       && (c.Code == "GHA" || c.Alpha2Code == "GH" || c.Name == "Ghana"), ct);

            calendar = new HolidayCalendar
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CalendarName = CalendarName,
                Description = "Public holidays gazetted under the Public Holidays Act, 2001 (Act 601).",
                CountryId = ghana?.Id,
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = By
            };

            _context.Set<HolidayCalendar>().Add(calendar);
        }

        // Two years: the current one so day-counting works today, and the next so a leave plan or a
        // request booked into next year is also costed correctly. A calendar that stops at 31
        // December silently starts counting holidays as leave days on 1 January.
        var thisYear = DateTime.Today.Year;
        var years = new[] { thisYear, thisYear + 1 };

        var existing = await _context.Set<PublicHoliday>().IgnoreQueryFilters()
            .Where(h => h.TenantId == tenantId && h.HolidayCalendarId == calendar.Id)
            .Select(h => new { h.HolidayName, h.DateFrom })
            .ToListAsync(ct);

        var known = existing
            .Select(h => (h.HolidayName, h.DateFrom))
            .ToHashSet();

        var added = 0;
        foreach (var year in years)
        {
            foreach (var (name, date, description) in GhanaHolidays(year))
            {
                if (!known.Add((name, date))) continue;

                _context.Set<PublicHoliday>().Add(new PublicHoliday
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    HolidayCalendarId = calendar.Id,
                    HolidayName = name,
                    Description = description,
                    DateFrom = date,
                    DateTo = date,
                    ObservanceType = HolidayObservanceType.Mandatory,

                    // Act 601 shifts a holiday falling on a Saturday or Sunday to the following
                    // Monday. Recording the substitute date rather than moving the holiday keeps the
                    // gazetted date visible, which is what the calendar is asked about.
                    SubstitutionDate = SubstituteFor(date),

                    AttractsHolidayPay = true,
                    HolidayPayMultiplier = 2.0m,

                    // False on purpose. Only some of these recur on a fixed date; Easter and the two
                    // Eids move every year, and a calendar that claims they repeat annually would be
                    // wrong for most of its entries. The seeder writes each year explicitly instead.
                    IsRecurringAnnually = false,

                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = By
                });

                added++;
            }
        }

        _logger.LogInformation(
            "Seeded {Added} public holidays for {Years}. The two Eid dates are estimates — they "
            + "depend on the lunar observation and are gazetted a few weeks ahead.",
            added, string.Join(" and ", years));
    }

    /// <summary>
    /// The gazetted public holidays for one year, under the Public Holidays Act, 2001 (Act 601).
    /// </summary>
    private static IEnumerable<(string Name, DateOnly Date, string Description)> GhanaHolidays(int year)
    {
        var easter = EasterSunday(year);

        yield return ("New Year's Day", new DateOnly(year, 1, 1), "Statutory public holiday.");
        yield return ("Constitution Day", new DateOnly(year, 1, 7),
            "Marks the coming into force of the 1992 Constitution.");
        yield return ("Independence Day", new DateOnly(year, 3, 6),
            "Independence from British rule, 1957.");
        yield return ("Good Friday", easter.AddDays(-2), "Moveable feast, tied to Easter.");
        yield return ("Easter Monday", easter.AddDays(1), "Moveable feast, tied to Easter.");
        yield return ("May Day", new DateOnly(year, 5, 1), "Workers' Day.");
        yield return ("Eid al-Fitr", EidAlFitrEstimate(year),
            "ESTIMATE — the date depends on the lunar observation and is gazetted shortly beforehand.");
        yield return ("Eid al-Adha", EidAlAdhaEstimate(year),
            "ESTIMATE — the date depends on the lunar observation and is gazetted shortly beforehand.");
        yield return ("Founders' Day", new DateOnly(year, 8, 4),
            "Commemorates the founding of the movement for independence.");
        yield return ("Kwame Nkrumah Memorial Day", new DateOnly(year, 9, 21),
            "Birthday of Ghana's first President.");
        yield return ("Farmers' Day", FirstFridayOfDecember(year),
            "National Farmers' Day, the first Friday in December.");
        yield return ("Christmas Day", new DateOnly(year, 12, 25), "Statutory public holiday.");
        yield return ("Boxing Day", new DateOnly(year, 12, 26), "Statutory public holiday.");
    }

    /// <summary>
    /// Act 601 moves a holiday falling at the weekend to the following Monday. Returns the substitute
    /// date, or null when the holiday already falls on a working day.
    /// </summary>
    private static DateOnly? SubstituteFor(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(2),
        DayOfWeek.Sunday => date.AddDays(1),
        _ => null
    };

    private static DateOnly FirstFridayOfDecember(int year)
    {
        var date = new DateOnly(year, 12, 1);
        while (date.DayOfWeek != DayOfWeek.Friday) date = date.AddDays(1);
        return date;
    }

    /// <summary>Easter Sunday, by the anonymous Gregorian computus.</summary>
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    // The two Eids follow the Hijri calendar, which is lunar and shorter than the Gregorian year, so
    // each falls roughly 11 days earlier every year and the exact day is confirmed by observation.
    // A computed approximation is honest about being one; a hardcoded date that silently goes stale
    // is not. Both are marked ESTIMATE in the description so the screen says so too.

    private static DateOnly EidAlFitrEstimate(int year) => ShiftFromAnchor(new DateOnly(2026, 3, 20), year);

    private static DateOnly EidAlAdhaEstimate(int year) => ShiftFromAnchor(new DateOnly(2026, 5, 27), year);

    /// <summary>
    /// Projects a known Hijri-linked date onto another Gregorian year by the ~10.875-day annual drift.
    /// Accurate to a day or two, which is all an estimate should claim.
    /// </summary>
    private static DateOnly ShiftFromAnchor(DateOnly anchor, int year)
    {
        var driftDays = (int)Math.Round((year - anchor.Year) * -10.875);
        var projected = anchor.AddDays(driftDays);

        // Keep the result inside the requested year; the drift can push it over a boundary.
        if (projected.Year != year)
        {
            projected = projected.AddDays(projected.Year < year ? 354 : -354);
        }

        return projected;
    }
}
