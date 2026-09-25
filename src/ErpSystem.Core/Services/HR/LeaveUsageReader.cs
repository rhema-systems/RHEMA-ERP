using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ILeaveUsageReader"/>
public sealed class LeaveUsageReader : ILeaveUsageReader
{
    private readonly IGenericRepository<LeaveRequest> _requests;
    private readonly IGenericRepository<LeaveType> _leaveTypes;
    private readonly IHrWorkingDayCalculator _workingDays;

    public LeaveUsageReader(
        IGenericRepository<LeaveRequest> requests,
        IGenericRepository<LeaveType> leaveTypes,
        IHrWorkingDayCalculator workingDays)
    {
        _requests = requests;
        _leaveTypes = leaveTypes;
        _workingDays = workingDays;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LeaveUsage>> ReadAsync(
        Guid tenantId, Guid leaveTypeId, DateOnly yearStart, DateOnly yearEnd, DateOnly through,
        IReadOnlyCollection<Guid>? employeeIds = null, CancellationToken ct = default)
    {
        var leaveType = await _leaveTypes
            .GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == leaveTypeId && t.TenantId == tenantId, ct)
            ?? throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        var query = _requests
            .GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.LeaveTypeId == leaveTypeId
                     && r.StartDate >= yearStart && r.StartDate <= yearEnd
                     // The three statuses that count as taken (LeaveService.CountsAsTaken), and Pending.
                     && (r.Status == LeaveStatus.Approved
                         || r.Status == LeaveStatus.InProgress
                         || r.Status == LeaveStatus.Completed
                         || r.Status == LeaveStatus.Pending));

        if (employeeIds is { Count: > 0 })
        {
            var ids = employeeIds.ToArray();
            query = query.Where(r => ids.Contains(r.EmployeeId));
        }

        var requests = await query
            .Select(r => new { r.EmployeeId, r.StartDate, r.EndDate, r.TotalDays, r.Status })
            .ToListAsync(ct);

        // Holidays matter only for leave straddling the date, and only up to it; loaded once, the
        // first time one is met. A wider set gives the same walk (see LeaveChargeableDays).
        IReadOnlySet<DateOnly>? holidays = null;

        var usage = new Dictionary<Guid, LeaveUsage>();
        foreach (var employee in requests.GroupBy(r => r.EmployeeId))
        {
            decimal takenThrough = 0m, takenOrBooked = 0m, pending = 0m;
            foreach (var r in employee)
            {
                if (r.Status == LeaveStatus.Pending)
                {
                    pending += r.TotalDays;
                    continue;
                }

                takenOrBooked += r.TotalDays;
                if (r.EndDate <= through)
                {
                    // Wholly on or before the date: as it was charged.
                    takenThrough += r.TotalDays;
                }
                else if (r.StartDate <= through)
                {
                    // Straddling it: its chargeable days up to the date, never more than it was charged.
                    holidays ??= await _workingDays.GetHolidayDatesAsync(tenantId, yearStart, through, ct);
                    takenThrough += Math.Min(
                        LeaveChargeableDays.Between(r.StartDate, through, leaveType, holidays).Count,
                        r.TotalDays);
                }
            }

            usage[employee.Key] = new LeaveUsage(takenThrough, takenOrBooked, pending);
        }

        return usage;
    }
}
