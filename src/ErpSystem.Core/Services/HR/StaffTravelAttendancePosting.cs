using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Puts a staff-travel trip's working days on the traveller's attendance register as <c>OnDuty</c>, and takes them off
/// again (travel final closure, lane 9, slice 9a — O-12, D-53, D-54). <c>OnDuty</c> had no writer before this; the
/// dashboard already counted it as present, and since this slice the monthly summary does too.
/// </summary>
/// <remarks>
/// <para><b>Leave's model.</b> <see cref="LeaveAttendancePostingService"/> is the pattern, rule for rule: a day is owned by
/// the request's id (<see cref="StaffDailyAttendance.StaffTravelRequestId"/>, D-53, as leave's <c>LeaveRequestId</c>) and
/// the status; a day with a punch, a note, another status or another source is never overwritten — first writer wins,
/// so a leave day stands against a trip and a trip day against leave (each side warns of the other); only an empty
/// absence is taken over; a day given up is hard-deleted (the unique index on employee and date is not filtered on
/// <c>IsDeleted</c>), unless someone has punched it since — then it is kept and unlinked for the attendance desk.</para>
///
/// <para><b>Which days (D-54).</b> Monday to Friday, less the public holidays on the tenant's calendar
/// (<see cref="IHrWorkingDayCalculator"/>), from departure to return — or to the day an early return was marked
/// completed. Held while the trip is approved, under way, completed or closed; a cancel, <i>did not travel</i> or
/// Request change gives them up.</para>
///
/// <para><b>Tenant-explicit and best-effort.</b> The tenant is the trip's, so the nightly sweep — nobody signed in —
/// uses it unchanged. Every caller has already committed its own change; a failure here is logged, never thrown, and
/// the nightly reconcile puts it right.</para>
/// </remarks>
public class StaffTravelAttendancePosting
{
    private const string Actor = "Travel";

    /// <summary>How far back the nightly reconcile looks, as leave's does — older attendance is not rewritten.</summary>
    public const int ReconcileLookbackDays = 14;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly ILogger<StaffTravelAttendancePosting> _logger;

    public StaffTravelAttendancePosting(
        IUnitOfWork unitOfWork, IHrWorkingDayCalculator workingDays, ILogger<StaffTravelAttendancePosting> logger)
    {
        _unitOfWork = unitOfWork;
        _workingDays = workingDays;
        _logger = logger;
    }

    /// <summary>What one reconcile did: days put on, days already there, days left alone (someone else's), days given up.</summary>
    public sealed record Result(int DaysAdded, int DaysHeld, int DaysSkipped, int DaysRemoved);

    /// <summary>A trip whose days belong on the register: approved, under way, completed or closed.</summary>
    public static bool HoldsDays(StaffTravelRequestStatus status) =>
        status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress
            or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed;

    private IGenericRepository<StaffDailyAttendance> Days => _unitOfWork.Repository<StaffDailyAttendance>();

    /// <summary>The working days the trip holds now — none unless <see cref="HoldsDays"/>.</summary>
    public async Task<IReadOnlyList<DateOnly>> DaysOnDutyAsync(StaffTravelRequest trip, CancellationToken cancellationToken = default)
    {
        if (!HoldsDays(trip.Status)) return Array.Empty<DateOnly>();
        var last = trip.TravelEndDate;
        // An early return: the desk marked it completed before its end (lane 1 refuses before its start).
        if (trip.Status is StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed
            && trip.CompletedAt is DateTime completedAt
            && DateOnly.FromDateTime(completedAt) < last)
            last = DateOnly.FromDateTime(completedAt);
        if (last < trip.TravelStartDate) return Array.Empty<DateOnly>();

        var holidays = await _workingDays.GetHolidayDatesAsync(trip.TenantId, trip.TravelStartDate, last, cancellationToken);
        var days = new List<DateOnly>();
        for (var day = trip.TravelStartDate; day <= last; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(day))
                days.Add(day);
        return days;
    }

    /// <summary>
    /// Brings the trip's days on the register in line with its status, and saves — after the caller's own commit. Never
    /// throws: a failure is logged and the nightly reconcile retries it. Returns null when it failed.
    /// </summary>
    public async Task<Result?> ReconcileAsync(StaffTravelRequest trip, CancellationToken cancellationToken = default)
    {
        try
        {
            if (trip.TenantId == Guid.Empty)
                throw new InvalidOperationException("A trip without a tenant cannot be posted to attendance.");
            var days = await DaysOnDutyAsync(trip, cancellationToken);
            var result = await PostAsync(trip, days, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (result.DaysAdded + result.DaysRemoved + result.DaysSkipped > 0)
                _logger.LogInformation(
                    "Travel {Reference}: attendance {Added} day(s) added, {Held} held, {Skipped} left to what was recorded, {Removed} given up",
                    trip.RequestNumber, result.DaysAdded, result.DaysHeld, result.DaysSkipped, result.DaysRemoved);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Travel {Reference}: its attendance days could not be brought in line; the nightly sweep will retry",
                trip.RequestNumber);
            return null;
        }
    }

    /// <summary>
    /// The nightly reconcile (the travel sweep): every trip holding days that ended within <see cref="ReconcileLookbackDays"/>
    /// or has not ended, and every trip that holds rows it should not (cancelled, sent back, rejected). Returns the days
    /// added and given up.
    /// </summary>
    public async Task<(int Trips, int Added, int Removed)> ReconcileRecentAsync(
        Guid tenantId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var floor = today.AddDays(-ReconcileLookbackDays);
        var holding = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.TravelEndDate >= floor
                            && (r.Status == StaffTravelRequestStatus.Approved || r.Status == StaffTravelRequestStatus.InProgress
                                || r.Status == StaffTravelRequestStatus.Completed || r.Status == StaffTravelRequestStatus.Closed))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var postedIds = Days.GetQueryable(d => d.TenantId == tenantId && d.StaffTravelRequestId != null
                                            && d.Status == StaffAttendanceStatus.OnDuty)
            .Select(d => d.StaffTravelRequestId!.Value);
        // Deleted trips included: GetQueryable() hides them with its own Where, which no IgnoreQueryFilters lifts.
        var stale = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && postedIds.Contains(r.Id)
                     && (r.IsDeleted
                         || !(r.Status == StaffTravelRequestStatus.Approved || r.Status == StaffTravelRequestStatus.InProgress
                              || r.Status == StaffTravelRequestStatus.Completed || r.Status == StaffTravelRequestStatus.Closed)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        int trips = 0, added = 0, removed = 0;
        foreach (var trip in holding.Concat(stale).GroupBy(t => t.Id).Select(g => g.First()))
        {
            var result = trip.IsDeleted
                ? await ReleaseAllAsync(trip, cancellationToken)
                : await ReconcileAsync(trip, cancellationToken);
            if (result is null) continue;
            trips++;
            added += result.DaysAdded;
            removed += result.DaysRemoved;
        }
        return (trips, added, removed);
    }

    /// <summary>A deleted trip holds no days.</summary>
    private async Task<Result?> ReleaseAllAsync(StaffTravelRequest trip, CancellationToken cancellationToken)
    {
        try
        {
            var result = await PostAsync(trip, Array.Empty<DateOnly>(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Travel {Reference}: the days of the deleted trip could not be given up", trip.RequestNumber);
            return null;
        }
    }

    /// <summary>
    /// Makes the trip's <c>OnDuty</c> days exactly <paramref name="days"/>: adds the missing, gives up the extra. Tracked;
    /// the caller saves (a given-up day is hard-deleted at once, as leave's).
    /// </summary>
    private async Task<Result> PostAsync(StaffTravelRequest trip, IReadOnlyList<DateOnly> days, CancellationToken cancellationToken)
    {
        int added = 0, held = 0, skipped = 0;
        if (days.Count > 0)
        {
            var from = days[0];
            var to = days[^1];
            var existing = await Days.GetQueryable()
                .Where(d => d.TenantId == trip.TenantId && d.EmployeeId == trip.EmployeeId
                         && d.AttendanceDate >= from && d.AttendanceDate <= to)
                .ToListAsync(cancellationToken);
            var byDate = existing.ToDictionary(d => d.AttendanceDate);

            foreach (var day in days)
            {
                if (byDate.TryGetValue(day, out var row))
                {
                    if (row.StaffTravelRequestId == trip.Id && row.Status == StaffAttendanceStatus.OnDuty)
                    {
                        held++;
                        continue;
                    }
                    // Never overwrite what someone recorded — a punch, a note, leave's day, a clerk's status, another
                    // trip's day. Only an empty absence says nothing, and is taken over (leave's rule).
                    var emptyAbsence = row.Status == StaffAttendanceStatus.Absent
                                       && row.ActualCheckInTime == null && row.ActualCheckOutTime == null
                                       && row.LeaveRequestId == null && row.StaffTravelRequestId == null
                                       && string.IsNullOrWhiteSpace(row.StatusReason) && string.IsNullOrWhiteSpace(row.Notes);
                    if (!emptyAbsence)
                    {
                        skipped++;
                        continue;
                    }
                    row.Status = StaffAttendanceStatus.OnDuty;
                    row.StaffTravelRequestId = trip.Id;
                    row.StatusReason = ReasonFor(trip);
                    row.UpdatedAt = DateTime.UtcNow;
                    row.UpdatedBy = Actor;
                    added++;
                    continue;
                }

                await Days.AddAsync(new StaffDailyAttendance
                {
                    Id = Guid.NewGuid(),
                    TenantId = trip.TenantId,
                    EmployeeId = trip.EmployeeId,
                    AttendanceDate = day,
                    DayOfWeek = day.DayOfWeek,
                    Status = StaffAttendanceStatus.OnDuty,
                    StatusReason = ReasonFor(trip),
                    StaffTravelRequestId = trip.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Actor,
                });
                added++;
            }
        }

        // The trip's OnDuty days are exactly its days now: whatever else carries its id goes (a shortened trip, a
        // cancelled one, an early return).
        var keep = days.ToHashSet();
        var mine = await Days.GetQueryable()
            .Where(d => d.TenantId == trip.TenantId && d.StaffTravelRequestId == trip.Id && d.Status == StaffAttendanceStatus.OnDuty)
            .ToListAsync(cancellationToken);
        var removed = 0;
        foreach (var row in mine.Where(r => !keep.Contains(r.AttendanceDate)))
            if (await ReleaseAsync(row, trip))
                removed++;

        return new Result(added, held, skipped, removed);
    }

    /// <summary>Gives up one day: hard-deleted, or — punched since — kept and unlinked. True when deleted.</summary>
    private async Task<bool> ReleaseAsync(StaffDailyAttendance row, StaffTravelRequest trip)
    {
        if (row.ActualCheckInTime != null || row.ActualCheckOutTime != null)
        {
            row.StaffTravelRequestId = null;
            row.StatusReason = Clip($"Staff travel {trip.RequestNumber} no longer covers this day, but attendance was recorded for it");
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = Actor;
            return false;
        }
        // ⚠ Hard, as leave's: a soft-deleted row would still hold the employee's slot for the date on the unique index.
        await Days.HardDeleteAsync(row);
        return true;
    }

    private static string ReasonFor(StaffTravelRequest trip) =>
        Clip($"On duty — staff travel {trip.RequestNumber} to {trip.DestinationCity}");

    private static string Clip(string text) => text.Length > 500 ? text[..500] : text;
}
