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
/// <para>The guests AND the organiser (lane 2a, D-11). The module's old organiser-only overlap check
/// had no caller and is gone; the people an interview collides with are everyone the meeting holds —
/// a board meeting has one organiser and twelve attendees.</para>
///
/// <para>Hardness turns on the person's own answer. Somebody who ACCEPTED a firm meeting has said
/// they will be there, so that is hard; an unanswered invitation, or an event still awaiting approval,
/// is soft — the check should say "they may be busy", not refuse.</para>
/// </remarks>
public sealed class CompanyEventCommitmentSource : IPanelistCommitmentSource
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audience;

    public CompanyEventCommitmentSource(IUnitOfWork unitOfWork, IHrAudienceResolver audience)
    {
        _unitOfWork = unitOfWork;
        _audience = audience;
    }

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

        // ⚠ The organiser is committed to the event whether or not they invited themselves (D-11, lane
        // 2a). Before this the diaries and the clash check read the guest list only, so the person
        // running the meeting looked free during it.
        var organised = await _unitOfWork.Repository<CompanyEvent>().GetQueryable()
            .Where(e => q.EmployeeIds.Contains(e.OrganizerId)
                     && e.TenantId == q.TenantId && !e.IsDeleted
                     && !e.IsCancelled && e.Status != EventStatus.Cancelled
                     && e.StartDate <= dayEnd && e.EndDate >= dayStart)
            .ToListAsync(ct);

        var commitments = new List<PanelistCommitment>();
        var counted = new HashSet<(Guid Employee, Guid Event)>();
        foreach (var p in rows)
        {
            counted.Add((p.EmployeeId!.Value, p.Event!.Id));
            AddDays(commitments, q, p.EmployeeId!.Value, p.Event!,
                accepted: p.InvitationStatus == InvitationStatus.Accepted,
                $"{p.Event!.EventName} ({p.Event.Status}, invitation {p.InvitationStatus})");
        }
        foreach (var ev in organised.Where(e => !counted.Contains((e.OrganizerId, e.Id))))
        {
            counted.Add((ev.OrganizerId, ev.Id));
            AddDays(commitments, q, ev.OrganizerId, ev, accepted: true, $"{ev.EventName} ({ev.Status}, organiser)");
        }

        // ⚠ The event's audience (lane 2c, D-16): an event on the company calendar is for its audience —
        // everyone, a unit, management — not only its guests. Each of them not already counted is
        // committed too, softly: nobody asked them to answer. Private and Confidential events, and
        // "selected guests" ones, reach nobody here.
        var forAudiences = await _unitOfWork.Repository<CompanyEvent>().GetQueryable()
            .Where(e => e.TenantId == q.TenantId && !e.IsDeleted
                     && !e.IsCancelled && e.Status != EventStatus.Cancelled
                     && e.ShowOnCompanyCalendar
                     && e.Visibility != EventVisibility.Private && e.Visibility != EventVisibility.Confidential
                     && e.StartDate <= dayEnd && e.EndDate >= dayStart)
            .ToListAsync(ct);
        foreach (var ev in forAudiences)
        {
            if (CompanyEventRules.CalendarAudienceOf(ev) is not { } rule) continue;
            var asked = q.EmployeeIds.Where(id => !counted.Contains((id, ev.Id))).ToList();
            if (asked.Count == 0) continue;
            var reached = await _audience.IncludedAmongForTenantAsync(q.TenantId, [rule], asked, ct);
            foreach (var employeeId in reached)
                AddDays(commitments, q, employeeId, ev, accepted: false, $"{ev.EventName} ({ev.Status}, for {AudienceLabel(rule)})");
        }

        return commitments;
    }

    private static string AudienceLabel(HrAudienceRule rule) => rule.TargetType switch
    {
        HrAudienceTargetType.AllEmployees => "all staff",
        HrAudienceTargetType.OrganizationUnit => "their unit",
        HrAudienceTargetType.Management => "management",
        _ => "its audience",
    };

    /// <summary>
    /// One commitment per day of the event inside the query (R4-10A.2): a timed event over several days
    /// keeps its hours each day.
    /// </summary>
    /// <remarks>
    /// <para>⚠ It used to build one window, from the first day, so days two onward never reached the
    /// clash check or the diaries.</para>
    ///
    /// <para>Hard when the person has said yes — the organiser always has — to a timed event that is
    /// firm (<see cref="CompanyEventRules.IsFirm"/>, F-41): an event needing no approval counts once
    /// scheduled, where before only Confirmed did and nothing but Approve set it.</para>
    /// </remarks>
    private static void AddDays(
        List<PanelistCommitment> commitments, PanelistCommitmentQuery q, Guid employeeId, CompanyEvent ev,
        bool accepted, string label)
    {
        var timed = !ev.IsAllDayEvent && ev.StartTime.HasValue && ev.EndTime.HasValue;
        var hard = accepted && timed && CompanyEventRules.IsFirm(ev);

        foreach (var (_, start, end) in CompanyEventRules.DailyWindows(ev, q.FromDate, q.ToDate))
        {
            // A timed day must actually overlap the asked-about window; an all-day one covers it.
            if (timed && !q.Overlaps(start, end)) continue;

            commitments.Add(new PanelistCommitment(
                employeeId, false, CommitmentKind.Event,
                hard ? CommitmentHardness.Hard : CommitmentHardness.Soft,
                label, start, end,
                IsDayGranular: !timed,
                Reference: ev.EventNumber));
        }
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
            var name = n.Schedule!.Program?.ProgramName ?? "Training";
            void Add(DateTime start, DateTime end, bool dayGranular)
            {
                if (!dayGranular && !q.Overlaps(start, end)) return;
                commitments.Add(new PanelistCommitment(
                    n.EmployeeId, false, CommitmentKind.Training, CommitmentHardness.Soft,
                    $"{name} ({n.Status})", start, end, dayGranular, n.NominationNumber));
            }

            // ⚠ Company-schedule lane 5b (F-23): EVERY session in the window, in date order. This reported one session
            // (`FirstOrDefault` over an unordered read, so not even reliably the first) — enough for the clash check's
            // hour on one day, but a diary over a fortnight showed one afternoon of a five-session course.
            var own = sessions.Where(x => x.ScheduleId == n.ScheduleId).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ToList();
            var timed = own.Where(x => x.StartTime is not null && x.EndTime is not null).ToList();
            if (timed.Count > 0)
            {
                foreach (var s in timed)
                    Add(s.Date.Date + s.StartTime!.Value, s.Date.Date + s.EndTime!.Value, false);
                continue;
            }

            // No timed session in the window: the course's own days, clipped to the window — never the whole window
            // (a two-day course used to fill a sixty-day diary).
            var first = n.Schedule.StartDate.Date > dayStart.Date ? n.Schedule.StartDate.Date : dayStart.Date;
            var last = n.Schedule.EndDate.Date < dayEnd.Date ? n.Schedule.EndDate.Date : dayEnd.Date;
            if (n.Schedule.StartTime is { } cs && n.Schedule.EndTime is { } ce)
            {
                // The course's daily hours, on each of its days in the window.
                for (var day = first; day <= last; day = day.AddDays(1))
                    Add(day + cs, day + ce, false);
            }
            else
            {
                Add(first, last.Add(TimeOnly.MaxValue.ToTimeSpan()), true);
            }
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
/// <para><b>A closure is attributed to the people it covers</b> (company-schedule final closure,
/// lane 1: R4-13.1). A company-wide one reaches every panelist, external ones included, since the
/// office is shut; a site closure reaches staff assigned to that site; a unit closure the staff of
/// that unit and everything beneath it. It used to reach everybody, so the Accra diary showed the
/// Tema site's closure. A partial closure is labelled as a working day. A yearly closure appears
/// on its repeat in the asked-about years (C-38).</para>
///
/// <para><b>Holidays come from the one definition</b> leave and the statutory clocks use (R4-10A.4):
/// the default calendar's active Mandatory and SubstituteDay holidays, with the day in lieu. This
/// used to read every holiday of every calendar, retired and optional ones included. A holiday is
/// the whole tenant's, so it reaches every panelist.</para>
/// </remarks>
public sealed class ClosureCommitmentSource : IPanelistCommitmentSource
{
    private readonly IHrClosureCalendar _closures;
    private readonly IHrWorkingDayCalculator _workingDays;

    public ClosureCommitmentSource(IHrClosureCalendar closures, IHrWorkingDayCalculator workingDays)
    {
        _closures = closures;
        _workingDays = workingDays;
    }

    public string SourceName => "closures and public holidays";

    public async Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery q, CancellationToken ct = default)
    {
        var everyone = q.EmployeeIds.Select(id => (Id: id, External: false))
            .Concat(q.ExternalAssociateIds.Select(id => (Id: id, External: true)))
            .ToList();
        if (everyone.Count == 0) return Array.Empty<PanelistCommitment>();

        var from = q.FromDate;
        var to = q.ToDate;
        var dayStart = q.DayStart;
        var dayEnd = q.DayEnd;
        var commitments = new List<PanelistCommitment>();

        var closures = await _closures.GetClosuresAsync(q.TenantId, from, to, ct);
        if (closures.Count > 0)
        {
            var coverage = await _closures.CoverageAsync(q.TenantId, closures, q.EmployeeIds, ct);
            foreach (var c in closures)
            {
                var companyWide = BusinessClosureRules.ScopeOf(c).Kind == ClosureScopeKind.Company;
                var covered = coverage[c.Id];
                var label = BusinessClosureRules.IsNonWorking(c)
                    ? $"Business closure: {c.Title}"
                    : $"Partial closure (a working day): {c.Title}";

                foreach (var occurrence in BusinessClosureRules.OccurrencesIn(c, from, to))
                {
                    // ⚠ Clipped to the asked-about window. A two-week shutdown reported as spanning the
                    // whole fortnight is right; reported as spanning the query is wrong the moment the
                    // query is one day.
                    var start = occurrence.Start.ToDateTime(TimeOnly.MinValue);
                    var end = occurrence.End.ToDateTime(TimeOnly.MinValue);
                    foreach (var (id, external) in everyone)
                    {
                        if (external ? !companyWide : !covered.Contains(id)) continue;
                        commitments.Add(new PanelistCommitment(
                            id, external, CommitmentKind.Closure, CommitmentHardness.Soft, label,
                            start > dayStart ? start : dayStart,
                            end < dayEnd ? end : dayEnd,
                            IsDayGranular: true));
                    }
                }
            }
        }

        // One commitment per run of consecutive days of the same holiday, as there was one per
        // holiday row before.
        var holidays = await _workingDays.GetHolidaysAsync(q.TenantId, from, to, ct);
        var runs = new List<(DateOnly First, DateOnly Last, string Label)>();
        foreach (var day in holidays)
        {
            var label = day.InLieu ? $"Day off in lieu of {day.Name}" : $"Public holiday: {day.Name}";
            if (runs.Count > 0 && runs[^1].Label == label && runs[^1].Last.AddDays(1) == day.Date)
                runs[^1] = (runs[^1].First, day.Date, label);
            else
                runs.Add((day.Date, day.Date, label));
        }

        foreach (var (first, last, label) in runs)
            foreach (var (id, external) in everyone)
                commitments.Add(new PanelistCommitment(
                    id, external, CommitmentKind.Holiday, CommitmentHardness.Soft, label,
                    first.ToDateTime(TimeOnly.MinValue),
                    last.ToDateTime(TimeOnly.MaxValue),
                    IsDayGranular: true));

        return commitments;
    }
}
