using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ILeaveAttendancePostingService"/>
public class LeaveAttendancePostingService : ILeaveAttendancePostingService
{
    private readonly IGenericRepository<StaffDailyAttendance> _dailyRepository;
    private readonly ILogger<LeaveAttendancePostingService> _logger;

    public LeaveAttendancePostingService(
        IGenericRepository<StaffDailyAttendance> dailyRepository,
        ILogger<LeaveAttendancePostingService> logger)
    {
        _dailyRepository = dailyRepository;
        _logger = logger;
    }

    /// <remarks>
    /// ⚠ No ambient tenant here on purpose — see the interface. The caller supplies it, so this
    /// works identically inside a request and inside the nightly reconciliation.
    /// </remarks>
    private static Guid RequireTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to post leave onto the attendance register.");
        return tenantId;
    }

    /// <summary>
    /// The reason written onto a day this service created, so the register says why the day is
    /// there rather than leaving a bare status somebody has to trace back.
    /// </summary>
    private static string ReasonFor(LeaveRequest request)
        => $"On leave — {request.LeaveType?.Name ?? "leave"} ({request.RequestNumber})";

    public async Task<LeaveAttendancePostingResult> PostAsync(
        LeaveRequest request, Guid tenantId, IReadOnlyList<DateOnly> chargeableDays,
        CancellationToken ct = default)
    {
        if (chargeableDays.Count == 0)
            return new LeaveAttendancePostingResult(0, 0);

        RequireTenant(tenantId);
        var from = chargeableDays[0];
        var to = chargeableDays[^1];

        // One query for the whole span rather than one per day: a month of leave is 20+ round trips
        // otherwise, and this runs inside the approval transaction.
        var existing = await _dailyRepository
            .GetQueryable()
            .Where(d => d.TenantId == tenantId
                     && d.EmployeeId == request.EmployeeId
                     && d.AttendanceDate >= from
                     && d.AttendanceDate <= to)
            .ToListAsync(ct);

        var byDate = existing.ToDictionary(d => d.AttendanceDate);

        var written = 0;
        var skipped = 0;

        foreach (var date in chargeableDays)
        {
            if (byDate.TryGetValue(date, out var row))
            {
                // Already ours — posting twice must not double-write. Approving, cancelling and
                // re-approving is an ordinary sequence and it has to land in the same place.
                if (row.LeaveRequestId == request.Id && row.Status == StaffAttendanceStatus.OnLeave)
                {
                    written++;
                    continue;
                }

                // ⚠ Never overwrite an observation. A row with a punch on it says the person was at
                // work that day; a row somebody has written a reason or a note on says a human made
                // a judgement about it. Either way leave is not entitled to silently replace it —
                // the day is reported back to the caller instead.
                //
                // The one row that IS safe to take over is an Absent day carrying nothing at all.
                // In this system a date with no row and an empty Absent row mean the same thing:
                // daily rows are only ever created by a punch or an import, so an empty Absent row
                // holds no information. That is also what makes the reversal below lossless.
                var isEmptyAbsence =
                    row.Status == StaffAttendanceStatus.Absent
                    && row.ActualCheckInTime == null
                    && row.ActualCheckOutTime == null
                    && row.LeaveRequestId == null
                    && string.IsNullOrWhiteSpace(row.StatusReason)
                    && string.IsNullOrWhiteSpace(row.Notes);

                if (!isEmptyAbsence)
                {
                    skipped++;
                    continue;
                }

                row.Status = StaffAttendanceStatus.OnLeave;
                row.LeaveRequestId = request.Id;
                row.StatusReason = ReasonFor(request);
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedBy = "Leave";
                await _dailyRepository.UpdateAsync(row);
                written++;
                continue;
            }

            await _dailyRepository.AddAsync(new StaffDailyAttendance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = request.EmployeeId,
                AttendanceDate = date,
                DayOfWeek = date.DayOfWeek,
                Status = StaffAttendanceStatus.OnLeave,
                StatusReason = ReasonFor(request),
                LeaveRequestId = request.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Leave"
            });
            written++;
        }

        if (skipped > 0)
        {
            _logger.LogWarning(
                "Leave request {number}: {skipped} of {total} day(s) already had attendance recorded and were left alone",
                request.RequestNumber, skipped, chargeableDays.Count);
        }

        return new LeaveAttendancePostingResult(written, skipped);
    }

    public async Task<int> ReverseAsync(Guid leaveRequestId, Guid tenantId, CancellationToken ct = default)
    {
        RequireTenant(tenantId);

        var rows = await _dailyRepository
            .GetQueryable()
            .Where(d => d.TenantId == tenantId
                     && d.LeaveRequestId == leaveRequestId
                     && d.Status == StaffAttendanceStatus.OnLeave)
            .ToListAsync(ct);

        var removed = 0;
        foreach (var row in rows)
        {
            // If somebody has punched against the day since it was posted, the day is no longer
            // ours to remove — the punch is the fact and the leave link is the stale part. Unlink it
            // and leave the row for the attendance desk to resolve.
            if (row.ActualCheckInTime != null || row.ActualCheckOutTime != null)
            {
                row.LeaveRequestId = null;
                row.StatusReason = "Leave was cancelled or moved, but attendance was recorded for this day";
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedBy = "Leave";
                await _dailyRepository.UpdateAsync(row);
                continue;
            }

            // Otherwise the row exists only because leave put it there, so it goes. Deleting rather
            // than reverting to Absent is the lossless choice for the same reason the post step
            // could take over an empty Absent row: with nothing on it, "no row" and "empty row" say
            // the same thing, and a spurious Absent day would make somebody look truant for leave
            // they never took.
            //
            // ⚠ HARD delete, and it must stay one. The unique index on
            // (TenantId, EmployeeId, AttendanceDate) is NOT filtered on IsDeleted, while
            // GetQueryable() hides soft-deleted rows — so a soft delete would leave an invisible row
            // still holding that employee's slot for that date. The next post of the same dates, or
            // the employee's next punch on that day, would then fail on the index against a row
            // nothing can see. These rows carry no human input, so there is nothing to preserve.
            await _dailyRepository.HardDeleteAsync(row);
            removed++;
        }

        return removed;
    }
}
