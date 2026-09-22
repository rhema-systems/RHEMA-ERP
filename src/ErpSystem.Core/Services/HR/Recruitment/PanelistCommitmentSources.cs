using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Recruitment;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Where a panelist may already be, when somebody proposes an interview.
//
//  ⚠ ONE SOURCE PER THING THE ORGANISATION TRACKS, and registering it in
//  HrModuleServiceRegistration is not optional. A source that exists and is not registered is a
//  commitment the check will never see, and the check answers "free" — which is the failure mode
//  this whole interface exists to stop. Before round 4 lane D there were three sources hard-coded
//  into one method; a panelist chairing a board meeting was, as far as recruitment knew, available.
//
//  ⚠ EVERY source scopes its own read to the tenant. The DbContext is registered without one, so
//  the global query filter is inert — the convention the whole HR module runs on.
//
//  ⚠ Hardness is a property of the SOURCE, not of the caller. See CommitmentHardness: hard means
//  confirmed AND time-precise, because that is the only combination where refusing is honest.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Other interview panels the same people already sit on. Hard: precise, and confirmed.</summary>
public sealed class InterviewPanelCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public InterviewPanelCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "interviews";

    private static readonly JobInterviewStatus[] Blocking =
    {
        JobInterviewStatus.Scheduled, JobInterviewStatus.Rescheduled, JobInterviewStatus.InProgress,
    };

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        // ⚠ Round 4, D5. Filter by DATE RANGE in the query and by the precise window in memory.
        // A clash check asks about one window on one day; the personal diary asks about a fortnight.
        // Comparing times in SQL only works when the range is a single day — across days, 09:00 on
        // Tuesday is not "after" 17:00 on Monday by time-of-day alone.
        var from = q.FromDate;
        var to = q.ToDate;
        var results = new List<PanelistCommitment>();

        // ⚠ ONE query for every panelist, not one per panelist (§ 3 defect 10). The old code called
        // GetByEmployeeIdAsync inside the per-employee loop while leave and travel — right beside
        // it — were already batch-loaded.
        if (q.EmployeeIds.Count > 0)
        {
            var rows = await _unitOfWork.Repository<JobInterviewPanelist>().GetQueryable()
                .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy)
                .Where(p => q.EmployeeIds.Contains(p.EmployeeId)
                         && p.TenantId == q.TenantId && !p.IsDeleted
                         && p.JobInterview != null
                         && !p.JobInterview.IsDeleted
                         && p.JobInterview.TenantId == q.TenantId
                         && p.JobInterview.Id != q.ExcludeInterviewId
                         && Blocking.Contains(p.JobInterview.Status)
                         && p.JobInterview.ScheduledDate >= from
                         && p.JobInterview.ScheduledDate <= to)
                .ToListAsync(ct);

            results.AddRange(rows
                .Select(p => Describe(p.EmployeeId, false, p.JobInterview!))
                .Where(c => q.Overlaps(c.Start, c.End)));
        }

        if (q.ExternalAssociateIds.Count > 0)
        {
            var rows = await _unitOfWork.Repository<JobInterviewExternalPanelist>().GetQueryable()
                .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy)
                .Where(p => q.ExternalAssociateIds.Contains(p.AssociateId)
                         && p.TenantId == q.TenantId && !p.IsDeleted
                         && p.JobInterview != null
                         && !p.JobInterview.IsDeleted
                         && p.JobInterview.TenantId == q.TenantId
                         && p.JobInterview.Id != q.ExcludeInterviewId
                         && Blocking.Contains(p.JobInterview.Status)
                         && p.JobInterview.ScheduledDate >= from
                         && p.JobInterview.ScheduledDate <= to)
                .ToListAsync(ct);

            results.AddRange(rows
                .Select(p => Describe(p.AssociateId, true, p.JobInterview!))
                .Where(c => q.Overlaps(c.Start, c.End)));
        }

        return results;
    }

    private static PanelistCommitment Describe(Guid subjectId, bool isExternal, JobInterview i)
    {
        // ⚠ The interview's OWN date, not the query's — over a range they are not the same day.
        var day = i.ScheduledDate.ToDateTime(TimeOnly.MinValue);
        var title = string.IsNullOrWhiteSpace(i.JobVacancy?.JobTitle) ? "an interview" : i.JobVacancy!.JobTitle;
        return new PanelistCommitment(
            subjectId, isExternal, CommitmentKind.Interview, CommitmentHardness.Hard,
            $"Interview panel for {title}", day + i.StartTime, day + i.EndTime,
            IsDayGranular: false, Reference: i.InterviewNumber);
    }
}

/// <summary>
/// Approved, pending or in-progress leave covering the day.
/// </summary>
/// <remarks>
/// ⚠ Soft, and that is deliberate. Leave is recorded by the DAY, so it cannot say whether the
/// 09:00–11:00 hour is free — and somebody on annual leave may well agree to come in for an hour.
/// Refusing would be the system overruling a person about their own time on day-granular evidence.
/// </remarks>
public sealed class LeaveCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public LeaveCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "leave";

    private static readonly LeaveStatus[] Active =
    {
        LeaveStatus.Approved, LeaveStatus.Pending, LeaveStatus.InProgress,
    };

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        if (q.EmployeeIds.Count == 0) return Array.Empty<PanelistCommitment>();
        var from = q.FromDate;
        var to = q.ToDate;

        var rows = await _unitOfWork.Repository<LeaveRequest>().GetQueryable()
            .Include(l => l.LeaveType)
            .Where(l => q.EmployeeIds.Contains(l.EmployeeId)
                     && l.TenantId == q.TenantId && !l.IsDeleted
                     && Active.Contains(l.Status)
                     && l.StartDate <= to && l.EndDate >= from)
            .ToListAsync(ct);

        return rows.Select(l => new PanelistCommitment(
            l.EmployeeId, false, CommitmentKind.Leave, CommitmentHardness.Soft,
            $"{l.LeaveType?.Name ?? "Leave"} ({l.Status})",
            l.StartDate.ToDateTime(TimeOnly.MinValue),
            l.EndDate.ToDateTime(TimeOnly.MaxValue),
            IsDayGranular: true, Reference: l.RequestNumber)).ToList();
    }
}

/// <summary>Approved, submitted or in-progress travel covering the day. Soft, for the same reason as leave.</summary>
public sealed class TravelCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public TravelCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "travel";

    private static readonly StaffTravelRequestStatus[] Active =
    {
        StaffTravelRequestStatus.Approved, StaffTravelRequestStatus.Submitted, StaffTravelRequestStatus.InProgress,
    };

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        if (q.EmployeeIds.Count == 0) return Array.Empty<PanelistCommitment>();
        var from = q.FromDate;
        var to = q.ToDate;

        var rows = await _unitOfWork.Repository<StaffTravelRequest>().GetQueryable()
            .Where(t => q.EmployeeIds.Contains(t.EmployeeId)
                     && t.TenantId == q.TenantId && !t.IsDeleted
                     && Active.Contains(t.Status)
                     && t.TravelStartDate <= to && t.TravelEndDate >= from)
            .ToListAsync(ct);

        return rows.Select(t => new PanelistCommitment(
            t.EmployeeId, false, CommitmentKind.Travel, CommitmentHardness.Soft,
            $"Travel ({t.Status})",
            t.TravelStartDate.ToDateTime(TimeOnly.MinValue),
            t.TravelEndDate.ToDateTime(TimeOnly.MaxValue),
            IsDayGranular: true, Reference: t.RequestNumber)).ToList();
    }
}

/// <summary>
/// Meetings and company events the panelist is a PARTICIPANT of — not merely ones they organise.
/// </summary>
/// <remarks>
/// <para>⚠ The company-schedule module's own <c>HasConflictingEventAsync</c> checks the ORGANIZER
/// only, which is the smallest useful part of the answer: the people whose diaries an interview
/// actually collides with are the ones invited to the meeting, and a board meeting has one
/// organiser and twelve attendees.</para>
///
/// <para>Hardness turns on the participant's own answer. Somebody who ACCEPTED a Confirmed meeting
/// has said they will be there, so that is hard. An unanswered invitation, a Declined one, or a
/// merely Scheduled event is soft — the check should say "they may be busy", not refuse.</para>
/// </remarks>
public sealed class CompanyEventCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public CompanyEventCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "meetings and events";

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        if (q.EmployeeIds.Count == 0) return Array.Empty<PanelistCommitment>();

        // A whole-day span, because an all-day event carries no times and must still be found.
        var dayStart = q.DayStart;
        var dayEnd = q.DayEnd;

        var rows = await _unitOfWork.Repository<EventParticipant>().GetQueryable()
            .Include(p => p.Event)
            .Where(p => p.EmployeeId != null && q.EmployeeIds.Contains(p.EmployeeId.Value)
                     && p.TenantId == q.TenantId && !p.IsDeleted
                     && p.InvitationStatus != InvitationStatus.Declined
                     && p.Event != null
                     && !p.Event.IsDeleted
                     && p.Event.TenantId == q.TenantId
                     && !p.Event.IsCancelled
                     && p.Event.Status != EventStatus.Cancelled
                     && p.Event.StartDate <= dayEnd && p.Event.EndDate >= dayStart)
            .ToListAsync(ct);

        var commitments = new List<PanelistCommitment>();
        foreach (var p in rows)
        {
            var ev = p.Event!;
            // ⚠ The event's own day, not the query's first — over a range they differ.
            var (start, end) = WindowOf(ev, DateOnly.FromDateTime(ev.StartDate));

            // An event with times must actually overlap the asked-about window; an all-day one covers it.
            if (!ev.IsAllDayEvent && ev.StartTime.HasValue && ev.EndTime.HasValue && !q.Overlaps(start, end))
                continue;

            var accepted = p.InvitationStatus == InvitationStatus.Accepted;
            var confirmed = ev.Status is EventStatus.Confirmed or EventStatus.InProgress;
            var hard = accepted && confirmed && !ev.IsAllDayEvent && ev.StartTime.HasValue;

            commitments.Add(new PanelistCommitment(
                p.EmployeeId!.Value, false, CommitmentKind.Event,
                hard ? CommitmentHardness.Hard : CommitmentHardness.Soft,
                $"{ev.EventName} ({ev.Status}, invitation {p.InvitationStatus})",
                start, end,
                IsDayGranular: ev.IsAllDayEvent || !ev.StartTime.HasValue,
                Reference: ev.EventNumber));
        }

        return commitments;
    }

    private static (DateTime Start, DateTime End) WindowOf(CompanyEvent ev, DateOnly date)
    {
        if (ev.IsAllDayEvent || !ev.StartTime.HasValue || !ev.EndTime.HasValue)
            return (date.ToDateTime(TimeOnly.MinValue), date.ToDateTime(TimeOnly.MaxValue));

        return (date.ToDateTime(TimeOnly.MinValue) + ev.StartTime.Value,
                date.ToDateTime(TimeOnly.MinValue) + ev.EndTime.Value);
    }
}

/// <summary>
/// Rooms the panelist has booked. Confirmed is hard; Tentative warns (decision D-5).
/// </summary>
/// <remarks>
/// ⚠ This is the one source whose module already refuses on its own account: <c>RoomBooking</c> has
/// a blocking double-booking check. Recruitment could schedule an interview straight over the hour
/// somebody had booked a room for and hear nothing about it.
/// </remarks>
public sealed class RoomBookingCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public RoomBookingCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "room bookings";

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        if (q.EmployeeIds.Count == 0) return Array.Empty<PanelistCommitment>();

        var rows = await _unitOfWork.Repository<RoomBooking>().GetQueryable()
            .Include(b => b.Room)
            .Where(b => q.EmployeeIds.Contains(b.BookedById)
                     && b.TenantId == q.TenantId && !b.IsDeleted
                     && !b.IsCancelled
                     && b.Status != BookingStatus.Cancelled
                     && b.StartDateTime < q.WindowEnd && b.EndDateTime > q.WindowStart)
            .ToListAsync(ct);

        return rows.Select(b => new PanelistCommitment(
            b.BookedById, false, CommitmentKind.RoomBooking,
            b.Status == BookingStatus.Confirmed ? CommitmentHardness.Hard : CommitmentHardness.Soft,
            $"Booked {b.Room?.RoomName ?? "a room"} ({b.Status})",
            b.StartDateTime, b.EndDateTime,
            IsDayGranular: false, Reference: b.BookingNumber)).ToList();
    }
}

/// <summary>
/// Training the panelist is nominated for, through the schedule's dates and the session times.
/// </summary>
/// <remarks>
/// ⚠ Soft even when Confirmed. A nomination is a plan, and training attendance is the thing HR most
/// often moves to free somebody for an interview — refusing would be the tail wagging the dog. The
/// warning still has to appear, because scheduling a panelist onto a course they are booked on is
/// exactly the mistake a diary is supposed to prevent.
/// </remarks>
public sealed class TrainingCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public TrainingCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "training";

    private static readonly NominationStatus[] Active =
    {
        NominationStatus.Approved, NominationStatus.Confirmed, NominationStatus.Waitlisted,
    };

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        if (q.EmployeeIds.Count == 0) return Array.Empty<PanelistCommitment>();

        var dayStart = q.DayStart;
        var dayEnd = q.DayEnd;

        var rows = await _unitOfWork.Repository<TrainingNomination>().GetQueryable()
            .Include(n => n.Schedule).ThenInclude(sch => sch.Program)
            .Where(n => q.EmployeeIds.Contains(n.EmployeeId)
                     && n.TenantId == q.TenantId && !n.IsDeleted
                     && Active.Contains(n.Status)
                     && n.Schedule != null
                     && !n.Schedule.IsDeleted
                     && n.Schedule.StartDate <= dayEnd && n.Schedule.EndDate >= dayStart)
            .ToListAsync(ct);

        if (rows.Count == 0) return Array.Empty<PanelistCommitment>();

        // The schedule carries the course's overall window; a session carries the day's hours. Use
        // the session where the day has one, because a five-day course with one afternoon session on
        // the Wednesday should not read as "busy all week".
        var scheduleIds = rows.Select(n => n.ScheduleId).Distinct().ToList();
        var sessions = await _unitOfWork.Repository<TrainingSession>().GetQueryable()
            .Where(x => scheduleIds.Contains(x.ScheduleId)
                     && x.TenantId == q.TenantId && !x.IsDeleted
                     && x.Date >= dayStart && x.Date <= dayEnd)
            .ToListAsync(ct);

        var commitments = new List<PanelistCommitment>();
        foreach (var n in rows)
        {
            // ⚠ Over a range there may be several sessions; the first one in the window is the one
            // to report, and its own date is what the times hang off.
            var session = sessions.FirstOrDefault(x => x.ScheduleId == n.ScheduleId);
            var sessionDay = session?.Date.Date ?? dayStart;
            var name = n.Schedule!.Program?.ProgramName ?? "Training";

            DateTime start, end;
            bool dayGranular;
            if (session?.StartTime is { } ss && session.EndTime is { } se)
            {
                (start, end, dayGranular) = (sessionDay + ss, sessionDay + se, false);
            }
            else if (n.Schedule.StartTime is { } cs && n.Schedule.EndTime is { } ce)
            {
                // The course's daily hours, on the first day of the asked-about window that it covers.
                var courseDay = n.Schedule.StartDate.Date > dayStart ? n.Schedule.StartDate.Date : dayStart;
                (start, end, dayGranular) = (courseDay + cs, courseDay + ce, false);
            }
            else
            {
                (start, end, dayGranular) = (dayStart, dayEnd, true);
            }

            if (!dayGranular && !q.Overlaps(start, end)) continue;

            commitments.Add(new PanelistCommitment(
                n.EmployeeId, false, CommitmentKind.Training, CommitmentHardness.Soft,
                $"{name} ({n.Status})", start, end, dayGranular, n.NominationNumber));
        }

        return commitments;
    }
}

/// <summary>
/// Days the business is closed, and public holidays.
/// </summary>
/// <remarks>
/// <para>⚠ These are not per-person and they are deliberately SOFT. A closure says the office is
/// shut, not that a named individual is unavailable — interviews do get held on a closure day, and
/// a virtual panel is unaffected by the building being locked. What matters is that the recruiter
/// is told, which is what C-5 (*"closures reach nothing"*) was about.</para>
///
/// <para>The commitment is attributed to every panelist in the query, because the closure applies to
/// the organisation rather than to a person: attributing it to nobody would mean it never appeared
/// on a row and the recruiter would never see it.</para>
/// </remarks>
public sealed class ClosureCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    public ClosureCommitmentSource(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public string SourceName => "closures and public holidays";

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        var everyone = q.EmployeeIds.Select(id => (Id: id, External: false))
            .Concat(q.ExternalAssociateIds.Select(id => (Id: id, External: true)))
            .ToList();
        if (everyone.Count == 0) return Array.Empty<PanelistCommitment>();

        var dayStart = q.DayStart;
        var dayEnd = q.DayEnd;
        var commitments = new List<PanelistCommitment>();

        var closures = await _unitOfWork.Repository<BusinessClosure>().GetQueryable()
            .Where(c => c.TenantId == q.TenantId && !c.IsDeleted
                     && c.StartDate <= dayEnd && c.EndDate >= dayStart)
            .ToListAsync(ct);

        // ⚠ Clipped to the asked-about window. A two-week shutdown reported as spanning the whole
        // fortnight is right; reported as spanning the query is wrong the moment the query is one day.
        foreach (var c in closures)
            foreach (var (id, external) in everyone)
                commitments.Add(new PanelistCommitment(
                    id, external, CommitmentKind.Closure, CommitmentHardness.Soft,
                    $"Business closure: {c.Title}",
                    c.StartDate > dayStart ? c.StartDate : dayStart,
                    c.EndDate < dayEnd ? c.EndDate : dayEnd,
                    IsDayGranular: true));

        var from = q.FromDate;
        var to = q.ToDate;
        var holidays = await _unitOfWork.Repository<PublicHoliday>().GetQueryable()
            .Where(h => h.TenantId == q.TenantId && !h.IsDeleted
                     && h.DateFrom <= to && h.DateTo >= from)
            .ToListAsync(ct);

        foreach (var h in holidays)
            foreach (var (id, external) in everyone)
                commitments.Add(new PanelistCommitment(
                    id, external, CommitmentKind.Holiday, CommitmentHardness.Soft,
                    $"Public holiday: {h.HolidayName}",
                    h.DateFrom.ToDateTime(TimeOnly.MinValue),
                    h.DateTo.ToDateTime(TimeOnly.MaxValue),
                    IsDayGranular: true));

        return commitments;
    }
}
