using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// The company calendar (company-schedule final closure lane 7, D-7) — one read for HR and for staff, the server deciding
/// what each caller may see — and an event as staff see it.
/// </summary>
/// <remarks>
/// <para><b>Who sees what.</b> The HR desk (<c>HR.Company.Read</c>): every live event, every closure, every calendar
/// milestone, the holidays and every room booking. Anyone else: the events whose calendar audience includes them
/// (<see cref="CompanyEventRules.CalendarAudienceOf"/> — never a Private or Confidential one, never one still awaiting
/// approval), the events they are invited to (a declined invitation too, so they can change their answer — once it has
/// been sent) or organise, the closures that cover them, the calendar milestones, the holidays, and their own room
/// bookings. Everybody gets their own leave, travel, interview panels and training as "Mine".</para>
///
/// <para><b>Once each.</b> Closures and holidays come in the company layer, never again in Mine; an event the caller is in
/// the audience of AND invited to is one entry, carrying their invitation; their bookings come once.</para>
///
/// <para><b>The room view (C-26, C-35).</b> Narrowed to one room, the desk sees its bookings in full; anyone else sees when
/// it is booked — their own bookings by name, everybody else's as "Booked", with nothing of the purpose or the booker
/// (lane 3c's rule).</para>
/// </remarks>
public interface ICompanyCalendarService
{
    /// <summary>The calendar between two dates, at most sixty days apart (the diary's own limit).</summary>
    Task<CompanyCalendarDto> GetCalendarAsync(
        Guid? actorEmployeeId, bool hrDesk, DateOnly from, DateOnly to, Guid? roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// An event as staff see it — for its organiser, its guests, its audience, or the HR desk; anyone else gets a lookup
    /// miss (<see cref="ArgumentException"/>), never a refusal that confirms it exists.
    /// </summary>
    Task<CalendarEventViewDto> GetEventAsync(Guid eventId, Guid? actorEmployeeId, bool hrDesk, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ICompanyCalendarService"/>
public class CompanyCalendarService : ICompanyCalendarService
{
    private const string EventKind = "Event";
    private const string ClosureKind = "Closure";
    private const string MilestoneKind = "Milestone";
    private const string HolidayKind = "Holiday";
    private const string BookingKind = "RoomBooking";
    private const string MineKind = "Mine";

    /// <summary>The diary kinds that are the caller's own and nowhere else on the calendar.</summary>
    private static readonly CommitmentKind[] MineKinds =
        [CommitmentKind.Leave, CommitmentKind.Travel, CommitmentKind.Interview, CommitmentKind.Training];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audience;
    private readonly IHrClosureCalendar _closures;
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly IPersonalScheduleService _diary;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyCalendarService> _logger;

    public CompanyCalendarService(
        IUnitOfWork unitOfWork,
        IHrAudienceResolver audience,
        IHrClosureCalendar closures,
        IHrWorkingDayCalculator workingDays,
        IPersonalScheduleService diary,
        ICurrentUserProvider currentUser,
        ILogger<CompanyCalendarService> logger)
    {
        _unitOfWork = unitOfWork;
        _audience = audience;
        _closures = closures;
        _workingDays = workingDays;
        _diary = diary;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ── the calendar ─────────────────────────────────────────────────────────────────────────────

    public async Task<CompanyCalendarDto> GetCalendarAsync(
        Guid? actorEmployeeId, bool hrDesk, DateOnly from, DateOnly to, Guid? roomId, CancellationToken cancellationToken = default)
    {
        if (to < from)
            throw new InvalidOperationException("The end of the range falls before its start.");
        if (to.DayNumber - from.DayNumber > 60)
            throw new InvalidOperationException("Sixty days is the most the calendar reads at once — narrow the range.");

        var tenantId = GetTenantId();
        var me = actorEmployeeId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var dayStart = from.ToDateTime(TimeOnly.MinValue);
        var dayEnd = to.ToDateTime(TimeOnly.MaxValue);
        var now = DateTime.UtcNow;

        var result = new CompanyCalendarDto { From = from, To = to, HrDesk = hrDesk, RoomId = roomId };

        await AddEventsAsync(result, tenantId, me, hrDesk, dayStart, dayEnd, now, cancellationToken);
        await AddClosuresAsync(result, tenantId, me, hrDesk, from, to, cancellationToken);
        await AddMilestonesAsync(result, tenantId, hrDesk, from, to, cancellationToken);
        await AddHolidaysAsync(result, tenantId, from, to, cancellationToken);
        await AddBookingsAsync(result, tenantId, me, hrDesk, dayStart, dayEnd, roomId, cancellationToken);

        if (me is { } employeeId)
        {
            var diary = await _diary.GetForEmployeeAsync(employeeId, from, to, cancellationToken);
            result.IncompleteSources = diary.IncompleteSources;
            foreach (var e in diary.Entries.Where(e => MineKinds.Contains(e.Kind)))
            {
                result.Entries.Add(new CalendarEntryDto
                {
                    Kind = MineKind,
                    SubKind = e.Kind.ToString(),
                    Label = e.Label,
                    Start = e.Start,
                    End = e.End,
                    IsAllDay = e.IsDayGranular,
                    ColourKey = $"{MineKind}.{e.Kind}",
                    Reference = e.Reference,
                });
            }
        }

        result.Entries = result.Entries.OrderBy(e => e.Start).ThenBy(e => e.Kind).ThenBy(e => e.Label).ToList();
        return result;
    }

    private async Task AddEventsAsync(
        CompanyCalendarDto result, Guid tenantId, Guid? me, bool hrDesk, DateTime dayStart, DateTime dayEnd, DateTime now,
        CancellationToken ct)
    {
        var live = _unitOfWork.Repository<CompanyEvent>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && !e.IsCancelled && e.Status != EventStatus.Cancelled
                     && e.StartDate <= dayEnd && e.EndDate >= dayStart);

        // The caller's own invitations in the range — for the desk too, whose calendar carries their answer.
        var mine = me is null
            ? new List<EventParticipant>()
            : await _unitOfWork.Repository<EventParticipant>().GetQueryable().AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.EmployeeId == me
                         && p.Event != null && !p.Event.IsDeleted && !p.Event.IsCancelled && p.Event.Status != EventStatus.Cancelled
                         && p.Event.StartDate <= dayEnd && p.Event.EndDate >= dayStart)
                .ToListAsync(ct);
        var invitationOf = mine.GroupBy(p => p.EventId).ToDictionary(g => g.Key, g => g.First());

        List<CompanyEvent> events;
        if (hrDesk)
        {
            events = await live.ToListAsync(ct);
        }
        else if (me is not null)
        {
            var employeeId = me.Value;
            var invitedIds = invitationOf
                .Where(kv => kv.Value.InvitationStatus != InvitationStatus.NotSent)
                .Select(kv => kv.Key).ToList();
            var candidates = await live
                .Where(e => e.OrganizerId == employeeId || invitedIds.Contains(e.Id)
                         || (e.ShowOnCompanyCalendar && e.Visibility != EventVisibility.Private && e.Visibility != EventVisibility.Confidential))
                .ToListAsync(ct);

            // An audience rule is resolved once for the caller, however many events share it.
            var reach = new Dictionary<HrAudienceRule, bool>();
            events = new List<CompanyEvent>();
            foreach (var e in candidates)
            {
                if (e.OrganizerId == employeeId || invitedIds.Contains(e.Id))
                {
                    events.Add(e);
                    continue;
                }
                if (CompanyEventRules.IsAwaitingApproval(e) || CompanyEventRules.CalendarAudienceOf(e) is not { } rule) continue;
                if (!reach.TryGetValue(rule, out var reached))
                {
                    reached = (await _audience.IncludedAmongForTenantAsync(tenantId, [rule], [employeeId], ct)).Contains(employeeId);
                    reach[rule] = reached;
                }
                if (reached) events.Add(e);
            }
        }
        else
        {
            return;
        }

        foreach (var e in events)
        {
            var window = EventWindow.Of(e);
            var timed = !e.IsAllDayEvent && e.StartTime.HasValue && e.EndTime.HasValue;
            var invitation = invitationOf.GetValueOrDefault(e.Id);
            var whyNot = invitation is null ? null : CompanyEventRules.RefuseSelfAnswer(e, invitation, now);
            result.Entries.Add(new CalendarEntryDto
            {
                Kind = EventKind,
                SubKind = e.Category.ToString(),
                Label = e.EventName,
                Start = window.Start,
                End = timed ? e.EndDate.Date + e.EndTime!.Value : e.EndDate.Date.Add(TimeOnly.MaxValue.ToTimeSpan()),
                IsAllDay = !timed,
                ColourKey = $"{EventKind}.{e.Category}",
                Reference = e.EventNumber,
                Link = hrDesk ? CompanyScheduleNotices.EventLink(e) : CompanyScheduleNotices.GuestLink(e),
                EventId = e.Id,
                SeriesId = e.RecurrenceSeriesId,
                Status = e.Status.ToString(),
                AwaitingApproval = CompanyEventRules.IsAwaitingApproval(e),
                IsOrganiser = me != null && e.OrganizerId == me,
                MyParticipantId = invitation?.Id,
                MyAnswer = invitation?.InvitationStatus.ToString(),
                CanAnswer = invitation is not null && whyNot is null,
                WhyNotAnswer = whyNot,
            });
        }
    }

    private async Task AddClosuresAsync(
        CompanyCalendarDto result, Guid tenantId, Guid? me, bool hrDesk, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var closures = await _closures.GetClosuresAsync(tenantId, from, to, ct);
        if (closures.Count == 0) return;

        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>? coverage = null;
        if (!hrDesk)
        {
            if (me is not { } employeeId) return;
            coverage = await _closures.CoverageAsync(tenantId, closures, [employeeId], ct);
        }

        foreach (var c in closures)
        {
            if (coverage is not null && (!coverage.TryGetValue(c.Id, out var covered) || covered.Count == 0)) continue;
            var full = BusinessClosureRules.IsNonWorking(c);
            foreach (var occurrence in BusinessClosureRules.OccurrencesIn(c, from, to))
            {
                result.Entries.Add(new CalendarEntryDto
                {
                    Kind = ClosureKind,
                    SubKind = full ? "Full" : "Partial",
                    Label = full ? c.Title : $"{c.Title} (reduced operations — a working day)",
                    Start = occurrence.Start.ToDateTime(TimeOnly.MinValue),
                    End = occurrence.End.ToDateTime(TimeOnly.MaxValue),
                    IsAllDay = true,
                    ColourKey = $"{ClosureKind}.{(full ? "Full" : "Partial")}",
                    Link = hrDesk ? "/administration/hr/company-schedule/closures" : null,
                });
            }
        }
    }

    /// <summary>
    /// The milestones marked to show on the calendar (lane 7: <c>ShowOnCalendar</c> was stored and read by nothing), each on
    /// its occurrence — a yearly one on every anniversary in the range.
    /// </summary>
    private async Task AddMilestonesAsync(
        CompanyCalendarDto result, Guid tenantId, bool hrDesk, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var milestones = await _unitOfWork.Repository<CompanyMilestone>().GetQueryable().AsNoTracking()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.ShowOnCalendar)
            .ToListAsync(ct);
        foreach (var m in milestones)
        {
            foreach (var day in CompanyMilestoneRules.OccurrencesIn(m, from, to))
            {
                var years = m.IsRecurringAnnually ? CompanyMilestoneRules.YearsSince(m, day) : 0;
                result.Entries.Add(new CalendarEntryDto
                {
                    Kind = MilestoneKind,
                    SubKind = m.Category.ToString(),
                    Label = years > 0 ? $"{m.Title} ({years} years)" : m.Title,
                    Start = day.ToDateTime(TimeOnly.MinValue),
                    End = day.ToDateTime(TimeOnly.MaxValue),
                    IsAllDay = true,
                    ColourKey = MilestoneKind,
                    Link = hrDesk ? "/administration/hr/company-schedule/milestones" : null,
                });
            }
        }
    }

    /// <summary>The public holidays and days in lieu, one band per run of the same holiday (as the diary draws them).</summary>
    private async Task AddHolidaysAsync(CompanyCalendarDto result, Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var runs = new List<(DateOnly First, DateOnly Last, string Name, bool InLieu)>();
        foreach (var day in await _workingDays.GetHolidaysAsync(tenantId, from, to, ct))
        {
            if (runs.Count > 0 && runs[^1].Name == day.Name && runs[^1].InLieu == day.InLieu && runs[^1].Last.AddDays(1) == day.Date)
                runs[^1] = (runs[^1].First, day.Date, day.Name, day.InLieu);
            else
                runs.Add((day.Date, day.Date, day.Name, day.InLieu));
        }
        foreach (var (first, last, name, inLieu) in runs)
        {
            result.Entries.Add(new CalendarEntryDto
            {
                Kind = HolidayKind,
                SubKind = inLieu ? "InLieu" : "Holiday",
                Label = inLieu ? $"Day off in lieu of {name}" : name,
                Start = first.ToDateTime(TimeOnly.MinValue),
                End = last.ToDateTime(TimeOnly.MaxValue),
                IsAllDay = true,
                ColourKey = HolidayKind,
            });
        }
    }

    private async Task AddBookingsAsync(
        CompanyCalendarDto result, Guid tenantId, Guid? me, bool hrDesk, DateTime dayStart, DateTime dayEnd, Guid? roomId,
        CancellationToken ct)
    {
        var query = _unitOfWork.Repository<RoomBooking>().GetQueryable().AsNoTracking()
            .Include(b => b.Room)
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && !b.IsCancelled && b.Status != BookingStatus.Cancelled
                     && b.StartDateTime <= dayEnd && b.EndDateTime >= dayStart);

        if (roomId is { } room)
        {
            var named = await _unitOfWork.Repository<MeetingRoom>().GetQueryable().AsNoTracking()
                .Where(r => r.Id == room && r.TenantId == tenantId && !r.IsDeleted)
                .Select(r => r.RoomName)
                .FirstOrDefaultAsync(ct);
            if (named is null) throw new ArgumentException("That room was not found.");
            result.RoomName = named;
            query = query.Where(b => b.RoomId == room);
            // Anyone may see when a room is held; staff only when (lane 3c) — live bookings, not a no-show's history.
            if (!hrDesk) query = query.Where(b => b.Status == BookingStatus.Tentative || b.Status == BookingStatus.Confirmed
                                               || (me != null && b.BookedById == me));
        }
        else if (!hrDesk)
        {
            if (me is not { } employeeId) return;
            query = query.Where(b => b.BookedById == employeeId);
        }

        foreach (var b in await query.ToListAsync(ct))
        {
            var isMine = me is { } employeeId && b.BookedById == employeeId;
            var shown = hrDesk || isMine;
            var roomName = b.Room?.RoomName ?? "Room";
            result.Entries.Add(new CalendarEntryDto
            {
                Kind = BookingKind,
                SubKind = b.Status.ToString(),
                Label = shown ? $"{roomName}: {b.Purpose}" : $"{roomName}: booked",
                Start = RoomBookingRules.AsUtc(b.StartDateTime),
                End = RoomBookingRules.AsUtc(b.EndDateTime),
                IsAllDay = false,
                ColourKey = isMine ? $"{BookingKind}.Mine" : BookingKind,
                Reference = shown ? b.BookingNumber : null,
                Link = hrDesk ? $"/hr/company-schedule/bookings/{b.Id}" : isMine ? CompanyScheduleNotices.BookingLink(b) : null,
                RoomId = b.RoomId,
                RoomName = roomName,
                IsMine = isMine,
                BookingId = shown ? b.Id : null,
            });
        }
    }

    // ── one event, as staff see it ───────────────────────────────────────────────────────────────

    public async Task<CalendarEventViewDto> GetEventAsync(
        Guid eventId, Guid? actorEmployeeId, bool hrDesk, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var me = actorEmployeeId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var e = await _unitOfWork.Repository<CompanyEvent>().GetQueryable().AsNoTracking()
            .Include(x => x.Organizer)
            .Include(x => x.SiteLocation)
            .Include(x => x.OrganizationUnit)
            .FirstOrDefaultAsync(x => x.Id == eventId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("That event was not found.");

        var invitation = me is { } employeeId
            ? await _unitOfWork.Repository<EventParticipant>().GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(p => p.EventId == e.Id && p.TenantId == tenantId && !p.IsDeleted && p.EmployeeId == employeeId, cancellationToken)
            : null;
        var isOrganiser = me is { } organiser && e.OrganizerId == organiser;

        var allowed = hrDesk || isOrganiser || invitation is not null;
        if (!allowed && me is { } audienceMember && !CompanyEventRules.IsAwaitingApproval(e)
            && CompanyEventRules.CalendarAudienceOf(e) is { } rule)
        {
            allowed = (await _audience.IncludedAmongForTenantAsync(tenantId, [rule], [audienceMember], cancellationToken)).Contains(audienceMember);
        }
        // ⚠ A lookup miss, not a refusal: whether an event exists is not anybody's to learn by asking.
        if (!allowed) throw new ArgumentException("That event was not found.");

        var rooms = await _unitOfWork.Repository<RoomBooking>().GetQueryable().AsNoTracking()
            .Include(b => b.Room)
            .Where(b => b.EventId == e.Id && b.TenantId == tenantId && !b.IsDeleted && !b.IsCancelled && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartDateTime)
            .ToListAsync(cancellationToken);
        var occurrences = e.RecurrenceSeriesId is { } series
            ? await _unitOfWork.Repository<CompanyEvent>().GetQueryable().AsNoTracking()
                .CountAsync(x => x.RecurrenceSeriesId == series && x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
            : (int?)null;
        var whyNot = invitation is null ? null : CompanyEventRules.RefuseSelfAnswer(e, invitation, DateTime.UtcNow);

        return new CalendarEventViewDto
        {
            Id = e.Id,
            EventNumber = e.EventNumber,
            EventName = e.EventName,
            Description = e.Description,
            Category = e.Category.ToString(),
            Type = e.Type.ToString(),
            Status = e.Status.ToString(),
            IsCancelled = e.IsCancelled || e.Status == EventStatus.Cancelled,
            CancellationReason = e.CancellationReason,
            AwaitingApproval = CompanyEventRules.IsAwaitingApproval(e),
            StartDate = e.StartDate,
            StartTime = e.StartTime,
            EndDate = e.EndDate,
            EndTime = e.EndTime,
            IsAllDay = e.IsAllDayEvent,
            When = CompanyEventRules.Describe(EventWindow.Of(e)),
            LocationType = e.LocationType.ToString(),
            SiteName = e.SiteLocation?.Name,
            VenueName = e.VenueName,
            VenueAddress = e.VenueAddress,
            OnlineMeetingLink = e.OnlineMeetingLink,
            // ⚠ Only to those it is for in person: the guests and the organiser — not the audience, not the desk's reader
            // here (the HR page carries it for the desk).
            MeetingPassword = invitation is not null || isOrganiser ? e.MeetingPassword : null,
            Rooms = rooms.Select(b => $"{b.Room?.RoomName ?? "Room"}, {RoomBookingRules.Describe(RoomBookingRules.AsUtc(b.StartDateTime), RoomBookingRules.AsUtc(b.EndDateTime))}").ToList(),
            OrganizerName = e.Organizer is null ? null : $"{e.Organizer.FirstName} {e.Organizer.LastName}".Trim(),
            IsOrganiser = isOrganiser,
            AudienceDescription = CompanyEventRules.DescribeAudience(e, e.OrganizationUnit?.Name),
            RequiresRsvp = e.RequiresRsvp,
            RsvpDeadline = e.RsvpDeadline,
            SeriesId = e.RecurrenceSeriesId,
            OccurrenceNumber = e.OccurrenceNumber,
            OccurrenceCount = occurrences,
            MyInvitation = invitation is null ? null : new MyInvitationDto
            {
                ParticipantId = invitation.Id,
                Status = invitation.InvitationStatus.ToString(),
                ResponseDate = invitation.ResponseDate,
                ResponseComments = invitation.ResponseComments,
                CanAnswer = whyNot is null,
                WhyNot = whyNot,
            },
            CanOpenHrPage = hrDesk,
        };
    }
}
