using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Builds the Attendance &amp; Time dashboard in one round trip.
///
/// Everything here is a projection or an aggregate — no entity is materialised just to be
/// counted. The daily trend and the chronic-absentee ranking are each one grouped query
/// rather than a loop over days or employees, since both would otherwise scale with the
/// window and the headcount.
/// </summary>
public class AttendanceDashboardService : IAttendanceDashboardService
{
    /// <summary>Window the chronic-absentee ranking looks back over.</summary>
    private const int RiskWindowDays = 30;

    // ── How the attendance rate is defined ───────────────────────────────────────────
    //
    // The three grouped queries below each repeat these two predicates inline rather than
    // sharing a helper: EF cannot inline an Expression<Func<>> into g.Count(...), and
    // extracting one would force client evaluation of the whole grouping.
    //
    // ATTENDED (numerator) — Present, OnDuty, Late, HalfDay, RemoteWork, or the IsRemoteWork
    //   flag. Lateness and half-days count because the person WAS at work; punctuality is
    //   measured separately and would otherwise be penalised twice, so a fully-staffed day
    //   where everyone arrived five minutes late would read as 0% attendance. Writing it as
    //   one predicate is also what stops a record that is both Present and IsRemoteWork
    //   being counted twice and pushing the rate above 100%.
    //
    // EXPECTED (denominator) — every status except Weekend, PublicHoliday and OffDay. Those
    //   are not attendance failures so they leave the calculation entirely. Approved leave
    //   IS in the denominator: it is a scheduled working day the person did not attend, and
    //   DaysOnLeave is reported alongside so the reason stays visible.

    private readonly IStaffDailyAttendanceRepository _dailyRepository;
    private readonly IStaffAttendanceLogRepository _logRepository;
    private readonly IStaffAttendanceRegularizationRepository _regularizationRepository;
    private readonly IStaffOvertimeRequestRepository _overtimeRepository;
    private readonly IRemoteWorkRequestRepository _remoteWorkRepository;
    private readonly IStaffAttendanceAlertRepository _alertRepository;
    private readonly IStaffAttendanceDeviceRepository _deviceRepository;
    private readonly IPayPeriodRepository _payPeriodRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AttendanceDashboardService> _logger;

    public AttendanceDashboardService(
        IStaffDailyAttendanceRepository dailyRepository,
        IStaffAttendanceLogRepository logRepository,
        IStaffAttendanceRegularizationRepository regularizationRepository,
        IStaffOvertimeRequestRepository overtimeRepository,
        IRemoteWorkRequestRepository remoteWorkRepository,
        IStaffAttendanceAlertRepository alertRepository,
        IStaffAttendanceDeviceRepository deviceRepository,
        IPayPeriodRepository payPeriodRepository,
        IEmployeeRepository employeeRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<AttendanceDashboardService> logger)
    {
        _dailyRepository = dailyRepository;
        _logRepository = logRepository;
        _regularizationRepository = regularizationRepository;
        _overtimeRepository = overtimeRepository;
        _remoteWorkRepository = remoteWorkRepository;
        _alertRepository = alertRepository;
        _deviceRepository = deviceRepository;
        _payPeriodRepository = payPeriodRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter
    // is inert. Following the RHEMA convention, every read below is scoped explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<AttendanceDashboardDto> GetDashboardAsync(
        DateOnly? asOf = null,
        int trendDays = 7,
        int riskListSize = 5,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Clamp rather than reject: these arrive from a query string and a silly value should
        // not fail the whole dashboard.
        trendDays = Math.Clamp(trendDays, 1, 90);
        riskListSize = Math.Clamp(riskListSize, 1, 50);

        var dto = new AttendanceDashboardDto
        {
            TotalEmployees = await _employeeRepository.GetQueryable()
                .CountAsync(e => e.TenantId == tenantId && e.IsActive, ct),
        };

        await PopulateTodaySnapshotAsync(dto, tenantId, today, ct);
        await PopulateCurrentPayPeriodAsync(dto, tenantId, today, ct);
        await PopulateQueuesAsync(dto, tenantId, ct);
        await PopulateAlertsAsync(dto, tenantId, ct);
        await PopulateDevicesAndLogsAsync(dto, tenantId, ct);
        dto.DailyTrend = await BuildDailyTrendAsync(tenantId, today, trendDays, ct);
        dto.ChronicAbsentees = await BuildChronicAbsenteesAsync(tenantId, today, riskListSize, ct);

        dto.ComputedAt = DateTime.UtcNow;
        return dto;
    }

    /// <summary>
    /// Today's headline counts, taken in one grouped pass so the day's rows are scanned once.
    /// </summary>
    private async Task PopulateTodaySnapshotAsync(
        AttendanceDashboardDto dto, Guid tenantId, DateOnly today, CancellationToken ct)
    {
        var counts = await _dailyRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AttendanceDate == today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                // "Present" here is the headline tile: strictly at-work-on-site today.
                Present = g.Count(r => r.Status == StaffAttendanceStatus.Present || r.Status == StaffAttendanceStatus.OnDuty),
                Absent = g.Count(r => r.Status == StaffAttendanceStatus.Absent),
                Late = g.Count(r => r.IsLate),
                OnLeave = g.Count(r => r.Status == StaffAttendanceStatus.OnLeave),
                Remote = g.Count(r => r.IsRemoteWork),
                // Attended drives the rate — see the note at the top of the class.
                Attended = g.Count(r =>
                    r.Status == StaffAttendanceStatus.Present ||
                    r.Status == StaffAttendanceStatus.OnDuty ||
                    r.Status == StaffAttendanceStatus.Late ||
                    r.Status == StaffAttendanceStatus.HalfDay ||
                    r.Status == StaffAttendanceStatus.RemoteWork ||
                    r.IsRemoteWork),
                Expected = g.Count(r =>
                    r.Status != StaffAttendanceStatus.Weekend &&
                    r.Status != StaffAttendanceStatus.PublicHoliday &&
                    r.Status != StaffAttendanceStatus.OffDay),
            })
            .FirstOrDefaultAsync(ct);

        if (counts == null) return;

        dto.PresentToday = counts.Present;
        dto.AbsentToday = counts.Absent;
        dto.LateToday = counts.Late;
        dto.OnLeaveToday = counts.OnLeave;
        dto.RemoteToday = counts.Remote;
        dto.AttendanceRateToday = counts.Expected == 0
            ? 0
            : Math.Round((decimal)counts.Attended * 100m / counts.Expected, 2);
    }

    private async Task PopulateCurrentPayPeriodAsync(
        AttendanceDashboardDto dto, Guid tenantId, DateOnly today, CancellationToken ct)
    {
        // The period covering today, preferring an open one if periods overlap.
        var period = await _payPeriodRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.StartDate <= today && p.EndDate >= today)
            .OrderBy(p => p.Status == PayPeriodStatus.Open ? 0 : 1)
            .ThenByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(ct);

        if (period == null) return;

        dto.CurrentPayPeriodName = period.PeriodName;
        dto.CurrentPayPeriodStart = period.StartDate;
        dto.CurrentPayPeriodEnd = period.EndDate;
        dto.CurrentPayPeriodStatus = period.Status;

        dto.TotalOvertimeHoursThisPeriod = await _dailyRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId
                        && r.AttendanceDate >= period.StartDate
                        && r.AttendanceDate <= period.EndDate
                        && r.IsOvertime)
            .SumAsync(r => r.OvertimeHours ?? 0m, ct);
    }

    /// <summary>The approval backlogs — what someone has to act on.</summary>
    private async Task PopulateQueuesAsync(AttendanceDashboardDto dto, Guid tenantId, CancellationToken ct)
    {
        dto.PendingOvertimeRequests = await _overtimeRepository.GetQueryable()
            .CountAsync(o => o.TenantId == tenantId && o.Status == OvertimeRequestStatus.Pending, ct);

        dto.ApprovedOvertimeRequests = await _overtimeRepository.GetQueryable()
            .CountAsync(o => o.TenantId == tenantId && o.Status == OvertimeRequestStatus.Approved, ct);

        dto.PendingRegularizations = await _regularizationRepository.GetQueryable()
            .CountAsync(r => r.TenantId == tenantId && r.Status == AttendanceRegularizationStatus.Pending, ct);

        dto.PendingRemoteWorkRequests = await _remoteWorkRepository.GetQueryable()
            .CountAsync(r => r.TenantId == tenantId && r.Status == RemoteWorkRequestStatus.Pending, ct);
    }

    private async Task PopulateAlertsAsync(AttendanceDashboardDto dto, Guid tenantId, CancellationToken ct)
    {
        var openAlerts = _alertRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.Status == AttendanceAlertStatus.Active);

        dto.ActiveCriticalAlerts = await openAlerts
            .CountAsync(a => a.Severity == AttendanceAlertSeverity.Critical, ct);

        dto.ActiveWarningAlerts = await openAlerts
            .CountAsync(a => a.Severity == AttendanceAlertSeverity.Warning, ct);

        // Most severe first, then most recent — the order someone would work them in.
        var topAlerts = await openAlerts
            .Include(a => a.Employee)
            .Include(a => a.AlertRule)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.TriggeredDate)
            .Take(5)
            .ToListAsync(ct);

        dto.TopAlerts = topAlerts.ToSummaryDtoList().ToList();
    }

    private async Task PopulateDevicesAndLogsAsync(
        AttendanceDashboardDto dto, Guid tenantId, CancellationToken ct)
    {
        dto.UnprocessedAttendanceLogs = await _logRepository.GetQueryable()
            .CountAsync(l => l.TenantId == tenantId && !l.IsProcessed, ct);

        dto.ActiveDevices = await _deviceRepository.GetQueryable()
            .CountAsync(d => d.TenantId == tenantId && d.IsActive, ct);

        dto.DevicesWithPendingSync = await _deviceRepository.GetQueryable()
            .CountAsync(d => d.TenantId == tenantId && d.IsActive && d.PendingSyncCount > 0, ct);
    }

    /// <summary>
    /// Attendance per day over the trailing window, as one grouped query. Days with no
    /// records at all are filled in as zeroes so the series has no gaps to plot around.
    /// </summary>
    private async Task<List<DailyAttendanceTrendDto>> BuildDailyTrendAsync(
        Guid tenantId, DateOnly today, int trendDays, CancellationToken ct)
    {
        var from = today.AddDays(-(trendDays - 1));

        var grouped = await _dailyRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AttendanceDate >= from && r.AttendanceDate <= today)
            .GroupBy(r => r.AttendanceDate)
            .Select(g => new
            {
                Date = g.Key,
                Present = g.Count(r => r.Status == StaffAttendanceStatus.Present || r.Status == StaffAttendanceStatus.OnDuty),
                Absent = g.Count(r => r.Status == StaffAttendanceStatus.Absent),
                Late = g.Count(r => r.IsLate),
                OnLeave = g.Count(r => r.Status == StaffAttendanceStatus.OnLeave),
                Remote = g.Count(r => r.IsRemoteWork),
                Attended = g.Count(r =>
                    r.Status == StaffAttendanceStatus.Present ||
                    r.Status == StaffAttendanceStatus.OnDuty ||
                    r.Status == StaffAttendanceStatus.Late ||
                    r.Status == StaffAttendanceStatus.HalfDay ||
                    r.Status == StaffAttendanceStatus.RemoteWork ||
                    r.IsRemoteWork),
                Expected = g.Count(r =>
                    r.Status != StaffAttendanceStatus.Weekend &&
                    r.Status != StaffAttendanceStatus.PublicHoliday &&
                    r.Status != StaffAttendanceStatus.OffDay),
            })
            .ToListAsync(ct);

        var byDate = grouped.ToDictionary(x => x.Date);
        var trend = new List<DailyAttendanceTrendDto>(trendDays);

        for (var offset = 0; offset < trendDays; offset++)
        {
            var date = from.AddDays(offset);
            byDate.TryGetValue(date, out var row);

            trend.Add(new DailyAttendanceTrendDto
            {
                Date = date,
                DayOfWeek = date.DayOfWeek,
                Present = row?.Present ?? 0,
                Absent = row?.Absent ?? 0,
                Late = row?.Late ?? 0,
                OnLeave = row?.OnLeave ?? 0,
                Remote = row?.Remote ?? 0,
                AttendanceRate = row is null || row.Expected == 0
                    ? 0
                    : Math.Round((decimal)row.Attended * 100m / row.Expected, 2),
            });
        }

        return trend;
    }

    /// <summary>
    /// Employees with the worst attendance over the trailing 30 days, worst first.
    ///
    /// Ranked by absences then lateness. Employees with no absences are excluded outright —
    /// a "risk" list that includes people with a clean record is noise.
    /// </summary>
    private async Task<List<AttendanceRiskEmployeeDto>> BuildChronicAbsenteesAsync(
        Guid tenantId, DateOnly today, int riskListSize, CancellationToken ct)
    {
        var from = today.AddDays(-RiskWindowDays);

        var ranked = await _dailyRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AttendanceDate >= from && r.AttendanceDate <= today)
            .GroupBy(r => r.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                AbsentDays = g.Count(r => r.Status == StaffAttendanceStatus.Absent),
                LateDays = g.Count(r => r.IsLate),
                Attended = g.Count(r =>
                    r.Status == StaffAttendanceStatus.Present ||
                    r.Status == StaffAttendanceStatus.OnDuty ||
                    r.Status == StaffAttendanceStatus.Late ||
                    r.Status == StaffAttendanceStatus.HalfDay ||
                    r.Status == StaffAttendanceStatus.RemoteWork ||
                    r.IsRemoteWork),
                Expected = g.Count(r =>
                    r.Status != StaffAttendanceStatus.Weekend &&
                    r.Status != StaffAttendanceStatus.PublicHoliday &&
                    r.Status != StaffAttendanceStatus.OffDay),
            })
            .Where(x => x.AbsentDays > 0)
            .OrderByDescending(x => x.AbsentDays)
            .ThenByDescending(x => x.LateDays)
            .Take(riskListSize)
            .ToListAsync(ct);

        if (ranked.Count == 0) return new List<AttendanceRiskEmployeeDto>();

        // One follow-up query for the names rather than a join that would drag the whole
        // employee row through the grouping.
        var employeeIds = ranked.Select(x => x.EmployeeId).ToList();
        var employees = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.Id))
            // No Include: the projection below pulls the department name directly, so an
            // Include would be dropped by EF anyway.
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.EmployeeNumber,
                DepartmentName = e.Department != null ? e.Department.Name : null,
            })
            .ToDictionaryAsync(e => e.Id, ct);

        return ranked
            .Select(x =>
            {
                employees.TryGetValue(x.EmployeeId, out var employee);
                return new AttendanceRiskEmployeeDto
                {
                    EmployeeId = x.EmployeeId,
                    EmployeeName = employee == null
                        ? string.Empty
                        : $"{employee.FirstName} {employee.LastName}".Trim(),
                    EmployeeNumber = employee?.EmployeeNumber ?? string.Empty,
                    DepartmentName = employee?.DepartmentName,
                    AbsentDaysLast30 = x.AbsentDays,
                    LateDaysLast30 = x.LateDays,
                    AttendancePercentageLast30 = x.Expected == 0
                        ? 0
                        : Math.Round((decimal)x.Attended * 100m / x.Expected, 2),
                };
            })
            .ToList();
    }
}
