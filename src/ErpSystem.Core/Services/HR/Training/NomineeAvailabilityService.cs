using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Aggregates conflicting commitments (leave, staff travel, other training) that overlap a proposed
/// training window, so nominations can be made with full visibility. Each source is queried in its own
/// guarded block so a failure in one does not suppress the others.
///
/// <para>Ported from the standalone HRApi. There it injected <c>ApplicationDbContext</c> directly; in
/// RHEMA the <c>Core/Services/HR</c> layer works through repository interfaces (see the sibling
/// <see cref="TrainingNominationService"/>), so the three reads go through the corresponding
/// repositories' <c>GetQueryable</c> instead of <c>DbContext.Set&lt;T&gt;()</c>.</para>
/// </summary>
public class NomineeAvailabilityService : INomineeAvailabilityService
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly IStaffTravelRequestRepository _travelRepository;
    private readonly ITrainingNominationRepository _nominationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<NomineeAvailabilityService> _logger;

    public NomineeAvailabilityService(
        ILeaveRepository leaveRepository,
        IStaffTravelRequestRepository travelRepository,
        ITrainingNominationRepository nominationRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<NomineeAvailabilityService> logger)
    {
        _leaveRepository = leaveRepository;
        _travelRepository = travelRepository;
        _nominationRepository = nominationRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<List<NomineeConflictDto>> CheckAsync(
        IEnumerable<Guid> employeeIds, DateTime from, DateTime to, Guid? excludeScheduleId, CancellationToken cancellationToken = default)
    {
        var ids = employeeIds.Distinct().ToList();
        var conflicts = new List<NomineeConflictDto>();
        if (ids.Count == 0) return conflicts;

        // Resolved outside the guarded blocks below so a missing tenant surfaces instead of being logged away.
        var tenantId = GetTenantId();

        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        // ── Leave (pending / approved / in-progress overlapping the window) ──
        try
        {
            var leaves = await _leaveRepository.GetQueryable(l => l.TenantId == tenantId
                    && ids.Contains(l.EmployeeId)
                    && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.InProgress)
                    && l.StartDate <= toDate && l.EndDate >= fromDate)
                .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
                .ToListAsync(cancellationToken);

            conflicts.AddRange(leaves.Select(l => new NomineeConflictDto
            {
                EmployeeId = l.EmployeeId,
                Source = "Leave",
                Description = "On leave",
                FromDate = l.StartDate.ToDateTime(TimeOnly.MinValue),
                ToDate = l.EndDate.ToDateTime(TimeOnly.MinValue)
            }));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Nominee availability: leave check failed"); }

        // ── Staff travel (submitted / approved / in-progress overlapping the window) ──
        try
        {
            var travels = await _travelRepository.GetQueryable(t => t.TenantId == tenantId
                    && ids.Contains(t.EmployeeId)
                    && (t.Status == StaffTravelRequestStatus.Submitted || t.Status == StaffTravelRequestStatus.Approved || t.Status == StaffTravelRequestStatus.InProgress)
                    && t.TravelStartDate <= toDate && t.TravelEndDate >= fromDate)
                .Select(t => new { t.EmployeeId, t.TravelStartDate, t.TravelEndDate })
                .ToListAsync(cancellationToken);

            conflicts.AddRange(travels.Select(t => new NomineeConflictDto
            {
                EmployeeId = t.EmployeeId,
                Source = "Travel",
                Description = "On staff travel",
                FromDate = t.TravelStartDate.ToDateTime(TimeOnly.MinValue),
                ToDate = t.TravelEndDate.ToDateTime(TimeOnly.MinValue)
            }));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Nominee availability: travel check failed"); }

        // ── Other training (live nomination to another overlapping schedule) ──
        try
        {
            var trainings = await _nominationRepository.GetQueryable(n => n.TenantId == tenantId
                    && ids.Contains(n.EmployeeId)
                    && (excludeScheduleId == null || n.ScheduleId != excludeScheduleId)
                    && n.Status != NominationStatus.Draft
                    && n.Status != NominationStatus.Rejected
                    && n.Status != NominationStatus.Withdrawn
                    && n.Schedule.StartDate <= to && n.Schedule.EndDate >= from)
                .Select(n => new { n.EmployeeId, ProgramName = n.Schedule.Program.ProgramName, n.Schedule.StartDate, n.Schedule.EndDate })
                .ToListAsync(cancellationToken);

            conflicts.AddRange(trainings.Select(t => new NomineeConflictDto
            {
                EmployeeId = t.EmployeeId,
                Source = "Training",
                Description = $"Already nominated: {t.ProgramName}",
                FromDate = t.StartDate,
                ToDate = t.EndDate
            }));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Nominee availability: training check failed"); }

        return conflicts;
    }
}
