using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.CompanySchedule;
using ErpSystem.Core.Services.HR.Extensions;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Company Event Service

public class CompanyEventService : ICompanyEventService
{
    private readonly ICompanyEventRepository _eventRepository;
    private readonly IEventParticipantRepository _participantRepository;
    private readonly IEventAttendanceRepository _attendanceRepository;
    private readonly IEventAttachmentRepository _attachmentRepository;
    private readonly IEventTaskRepository _taskRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanyEventService> _logger;

    /// <summary>
    /// Round 4, D6 — the module could invite, reschedule and cancel, and told nobody.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>EventParticipant.InvitationSentDate</c> was written by the participant-create and meant
    /// nothing: no invitation was ever sent. An event with an RSVP deadline and no invitation is a
    /// deadline the invitee has never heard of.
    /// </remarks>
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICompanyHrPolicySettingsService _policySettings;

    /// <summary>Approval on the workflow engine (lane 2b, D-10).</summary>
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;

    /// <summary>The engine's entity-type key — registered in the catalogue, the display service and the seeder.</summary>
    private const string WorkflowEntityType = "CompanyEvent";

    /// <summary>Who an event is for (lane 2c, D-16), and the intranet announcement of it.</summary>
    private readonly IHrAudienceResolver _audience;
    private readonly IHrAnnouncementService _announcements;

    public CompanyEventService(
        ICompanyEventRepository eventRepository,
        IEventParticipantRepository participantRepository,
        IEventAttendanceRepository attendanceRepository,
        IEventAttachmentRepository attachmentRepository,
        IEventTaskRepository taskRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ITemplatedEmailService templatedEmail,
        ILogger<CompanyEventService> logger,
        ICompanyHrPolicySettingsService policySettings,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        IHrAudienceResolver audience,
        IHrAnnouncementService announcements)
    {
        _audience = audience;
        _announcements = announcements;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
        _policySettings = policySettings;
        _eventRepository = eventRepository;
        _participantRepository = participantRepository;
        _attendanceRepository = attendanceRepository;
        _attachmentRepository = attachmentRepository;
        _taskRepository = taskRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _templatedEmail = templatedEmail;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  D6 — telling people (round 4)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>The when-and-where tokens every company-schedule email shares.</summary>
    /// <remarks>
    /// ⚠ <c>EventTime</c> is left NULL for an all-day event rather than filled with 00:00–00:00.
    /// The template hides the line when the token is absent, so an all-day event reads "When:
    /// Tuesday, 14 October" — printing a time there would invent a precision the record does not
    /// carry, the same distinction the clash check draws between precise and day-granular.
    /// </remarks>
    private static Dictionary<string, string?> EventTokens(CompanyEvent ev, string participantName)
    {
        string? time = null;
        if (!ev.IsAllDayEvent && ev.StartTime.HasValue && ev.EndTime.HasValue)
            // ⚠ Verbatim: a TimeSpan format needs `hh\:mm`, and `\:` is not a legal escape
            // in an ordinary interpolated string.
            time = $@"{ev.StartTime.Value:hh\:mm} – {ev.EndTime.Value:hh\:mm}";

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ParticipantName"]   = participantName,
            ["EventName"]         = ev.EventName,
            ["EventNumber"]       = ev.EventNumber,
            ["EventDate"]         = ev.StartDate.ToString("dddd, d MMMM yyyy"),
            ["EventTime"]         = time,
            ["VenueName"]         = ev.VenueName,
            ["OnlineMeetingLink"] = ev.OnlineMeetingLink,
            ["OrganizerName"]     = ev.Organizer is null
                ? null
                : $"{ev.Organizer.FirstName} {ev.Organizer.LastName}".Trim(),
            ["Description"]       = ev.Description,
            ["RsvpDeadline"]      = ev.RsvpDeadline?.ToString("dddd, d MMMM yyyy"),
        };
    }

    /// <summary>
    /// Sends one company-schedule email without ever failing the operation that prompted it.
    /// </summary>
    /// <remarks>
    /// ⚠ Best-effort, and raced against a timeout, exactly as the recruitment sends are. The event
    /// is already committed by the time these run: a participant whose invitation cannot be
    /// delivered is still a participant, and an unreachable SMTP server must not roll back a
    /// meeting. Latency — not an exception — is the failure mode that matters, because
    /// TemplatedEmailService already swallows delivery failures and returns false.
    /// </remarks>
    private async Task SendEventEmailAsync(
        Guid tenantId, string eventKey, string? toEmail, Dictionary<string, string?> tokens, string description)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        try
        {
            // ⚠ By the EVENT'S tenant (round 4, lane N-b2): the reminder sweep sends with nobody signed
            // in, and without naming the tenant it would skip the tenant's own wording and print the
            // configuration's company name.
            var send = _templatedEmail.SendForTenantAsync(
                tenantId, CompanyScheduleEmailCatalog.Module, eventKey, toEmail, tokens);

            if (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10))) == send)
                await send;
            else
                _logger.LogWarning(
                    "{Description} email timed out after 10 s for {Email} — the operation itself succeeded.",
                    description, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send the {Description} email to {Email} — the operation itself succeeded.",
                description, toEmail);
        }
    }

    /// <summary>
    /// Tells every participant of an event something — a reschedule, a cancellation, a reminder.
    /// </summary>
    /// <remarks>
    /// ⚠ Reads the participants fresh rather than through the event's navigation, which callers may
    /// not have loaded. A notification loop over an empty unloaded collection tells nobody and looks
    /// like success.
    /// </remarks>
    private async Task<int> NotifyParticipantsAsync(
        CompanyEvent ev, string eventKey, Func<Dictionary<string, string?>, Dictionary<string, string?>>? enrich,
        string description, Func<EventParticipant, bool>? filter = null, CancellationToken cancellationToken = default)
    {
        var tenantId = ev.TenantId;
        var participants = (await _participantRepository.GetQueryable()
                .Include(p => p.Employee)
                .Where(p => p.EventId == ev.Id && p.TenantId == tenantId && !p.IsDeleted)
                .ToListAsync(cancellationToken))
            .Where(p => filter is null || filter(p))
            .ToList();

        var sent = 0;
        foreach (var p in participants)
        {
            var name = p.Employee is not null
                ? $"{p.Employee.FirstName} {p.Employee.LastName}".Trim()
                : p.ExternalParticipantName ?? "Colleague";
            var email = p.Employee?.EmailAddress ?? p.ExternalParticipantEmail;
            if (string.IsNullOrWhiteSpace(email)) continue;

            var tokens = EventTokens(ev, name);
            if (enrich is not null) tokens = enrich(tokens);

            await SendEventEmailAsync(ev.TenantId, eventKey, email, tokens, description);
            sent++;
        }

        _logger.LogInformation(
            "Company schedule: {Description} sent to {Count} participant(s) of {EventNumber}.",
            description, sent, ev.EventNumber);
        return sent;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<CompanyEvent> GetOwnedEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.SiteLocation)
            .Include(e => e.ApprovedBy)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company event with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// This tenant's events, with the names a list shows (lane 2a, F-30).
    /// </summary>
    /// <remarks>
    /// ⚠ The tenant is inside the query. The repository's list reads had none — the DbContext's own
    /// tenant filter is inert, as noted above — so they loaded every tenant's events and filtered here.
    /// They are gone; the lists are built on this.
    /// </remarks>
    private IQueryable<CompanyEvent> TenantEvents(Guid tenantId) =>
        _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.SiteLocation)
            .Where(e => e.TenantId == tenantId);

    public async Task<CompanyEventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<CompanyEventDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.Department)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.SiteLocation)
            .Include(e => e.ApprovedBy)
            .Include(e => e.Participants).ThenInclude(p => p.Employee)
            .Include(e => e.AttendanceRecords).ThenInclude(a => a.Employee)
            .Include(e => e.Attachments)
            .Include(e => e.Tasks).ThenInclude(t => t.AssignedTo)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company event with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<CompanyEventDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // The register shows the site; TenantEvents includes it — without it the register read blank
        // while the detail page showed it, which looked like missing data rather than a missing Include.
        var entities = await TenantEvents(GetTenantId()).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CompanyEventDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = TenantEvents(GetTenantId());

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyEventDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    /// <remarks>
    /// ⚠ By OVERLAP (lane 2a): every event that touches the range. The repository's read wanted the
    /// event to fit inside it, so a conference running into the range from the day before was missing.
    /// </remarks>
    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var from = startDate.Date;
        var toExclusive = endDate.Date.AddDays(1);
        var entities = await TenantEvents(GetTenantId())
            .Where(e => e.StartDate < toExclusive && e.EndDate >= from)
            .OrderBy(e => e.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByOrganizerAsync(Guid organizerId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantEvents(GetTenantId())
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantEvents(GetTenantId())
            .Where(e => e.OrganizationUnitId == organizationUnitId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByStatusAsync(EventStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await TenantEvents(GetTenantId())
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyEventSummaryDto>> GetByCategoryAsync(EventCategory category, CancellationToken cancellationToken = default)
    {
        var entities = await TenantEvents(GetTenantId())
            .Where(e => e.Category == category)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    /// <remarks>
    /// Live events touching the next <paramref name="daysAhead"/> days, today by UTC — the repository
    /// used the server's local date (the F-5 shape) and missed an event already under way.
    /// </remarks>
    public async Task<IEnumerable<CompanyEventSummaryDto>> GetUpcomingEventsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(Math.Max(0, daysAhead));
        var entities = await TenantEvents(GetTenantId())
            .Where(e => !e.IsCancelled && e.Status != EventStatus.Cancelled
                        && e.EndDate >= today && e.StartDate <= horizon)
            .OrderBy(e => e.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2a — windows, references and the lifecycle (final closure)
    // ═════════════════════════════════════════════════════════════════════════

    private static void Refuse(string? refusal)
    {
        if (refusal != null) throw new InvalidOperationException(refusal);
    }

    /// <summary>How an event that can no longer change is named in a refusal.</summary>
    private static string ClosedState(CompanyEvent e) =>
        e.IsCancelled || e.Status == EventStatus.Cancelled ? "cancelled" : "completed";

    private static void ApplyWindow(CompanyEvent e, EventWindow w)
    {
        e.StartDate = w.StartDate.Date;
        e.StartTime = w.StartTime;
        e.EndDate = w.EndDate.Date;
        e.EndTime = w.EndTime;
        e.IsAllDayEvent = w.AllDay;
    }

    /// <summary>
    /// Checks the window and the reply and reminder settings hung off it, drops what does not apply, and
    /// writes the window onto the event (lane 2a). Refuses with a sentence — a 422.
    /// </summary>
    private static void ValidateWindow(CompanyEvent e, EventWindow window)
    {
        var deadline = e.RsvpDeadline;
        var days = e.ReminderDaysBefore;
        Refuse(CompanyEventRules.ValidateAndNormalise(ref window, e.RequiresRsvp, ref deadline, e.SendReminders, ref days));
        ApplyWindow(e, window);
        e.RsvpDeadline = deadline;
        e.ReminderDaysBefore = days;
    }

    /// <summary>The window and settings, the retired department, the unit an event for a unit needs, and what it points at.</summary>
    private async Task ValidateAsync(
        CompanyEvent e, EventWindow window, Guid tenantId, bool checkOrganiser, CancellationToken cancellationToken)
    {
        if (e.DepartmentId is not null)
            throw new InvalidOperationException(
                "Events are for an organisation unit now, not a department. Choose the organisation unit instead.");

        ValidateWindow(e, window);

        if (e.Scope == ParticipantScope.Department && e.OrganizationUnitId is null)
            throw new InvalidOperationException(
                "An event for a unit needs the unit. Choose the organisation unit, or choose another audience.");

        await EnsureReferencesAsync(e, tenantId, checkOrganiser, cancellationToken);
    }

    /// <summary>
    /// The site and the unit must be this tenant's and live; a new organiser must be an active employee
    /// of this tenant (lane 2a, D-11).
    /// </summary>
    /// <remarks>
    /// <para>⚠ Before this, an unknown id failed at the database as a 500, and another tenant's id was
    /// STORED: its foreign key is satisfied, and the DbContext's tenant filter is inert (F-10).</para>
    ///
    /// <para>The organiser is checked only when it is new, so an event whose organiser has since left can
    /// still be edited — and handed to someone else.</para>
    /// </remarks>
    private async Task EnsureReferencesAsync(CompanyEvent e, Guid tenantId, bool checkOrganiser, CancellationToken cancellationToken)
    {
        if (e.LocationId is { } locationId
            && !await _unitOfWork.Repository<Location>().GetQueryable()
                .AnyAsync(l => l.Id == locationId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken))
            throw new InvalidOperationException("The site chosen for this event was not found. Choose the site again.");

        if (e.OrganizationUnitId is { } unitId
            && !await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .AnyAsync(u => u.Id == unitId && u.TenantId == tenantId && !u.IsDeleted, cancellationToken))
            throw new InvalidOperationException(
                "The organisation unit chosen for this event was not found. Choose the unit again.");

        if (!checkOrganiser) return;
        var organiser = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(x => x.Id == e.OrganizerId && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (organiser is null)
            throw new InvalidOperationException("The organiser chosen was not found. Choose the organiser again.");
        if (!organiser.IsActive)
            throw new InvalidOperationException(
                "The organiser chosen is no longer an active employee. Choose someone who is.");
    }

    /// <summary>
    /// Records who created the event (D-11).
    /// </summary>
    /// <remarks>
    /// ⚠ Nothing stamped it before lane 2a — <c>CreatedBy</c> was null on every event on UAT — so once the
    /// organiser could be somebody else, who made the event would have been lost.
    /// </remarks>
    private void StampCreator(CompanyEvent e)
    {
        var userId = _currentUserProvider.UserId;
        e.CreatedById = userId == Guid.Empty ? null : userId;
        e.CreatedBy = !string.IsNullOrWhiteSpace(_currentUserProvider.FullName) ? _currentUserProvider.FullName
            : !string.IsNullOrWhiteSpace(_currentUserProvider.Username) ? _currentUserProvider.Username
            : e.CreatedById?.ToString();
    }

    /// <summary>
    /// The status an edit may set (F-37): scheduled, in progress or postponed — and confirmed where no
    /// approval is needed, or it has been given.
    /// </summary>
    /// <remarks>
    /// ⚠ The edit wrote any status. Confirmed made an unapproved event look approved — and firm in the
    /// clash check; Cancelled left <c>IsCancelled</c> unset; an "un-cancel" left it set.
    /// </remarks>
    private static void ApplyStatus(CompanyEvent e, EventStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new InvalidOperationException("Choose the status: scheduled, in progress or postponed.");

        switch (status)
        {
            case EventStatus.Cancelled:
                throw new InvalidOperationException(
                    "Cancel the event with Cancel, which asks for the reason and tells everybody invited.");
            case EventStatus.Completed:
                throw new InvalidOperationException("Complete the event with Complete, once it has taken place.");
            case EventStatus.Rescheduled:
                throw new InvalidOperationException(
                    "An event becomes Rescheduled when its dates move. Change the dates, or use Reschedule.");
            case EventStatus.Confirmed when CompanyEventRules.IsAwaitingApproval(e):
                throw new InvalidOperationException(
                    $"{e.EventName} needs approval. Approve it rather than marking it confirmed.");
        }

        e.Status = status;
    }

    /// <summary>The event's room bookings that are still live.</summary>
    private IQueryable<RoomBooking> LiveLinkedBookings(CompanyEvent e) =>
        _unitOfWork.Repository<RoomBooking>().GetQueryable()
            .Where(b => b.EventId == e.Id && b.TenantId == e.TenantId && !b.IsDeleted && !b.IsCancelled
                        && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Completed
                        && b.Status != BookingStatus.NoShow);

    /// <summary>
    /// Cancels the event's live room bookings with it (F-39): a cancelled or deleted meeting must not
    /// keep its room.
    /// </summary>
    private async Task<List<string>> CancelLinkedBookingsAsync(CompanyEvent e, string reason, CancellationToken cancellationToken)
    {
        var linked = await LiveLinkedBookings(e).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var b in linked)
        {
            b.IsCancelled = true;
            b.CancellationDate = now;
            b.CancellationReason = reason.Length > 1000 ? reason[..1000] : reason;
            b.Status = BookingStatus.Cancelled;
        }
        return linked.Select(b => b.BookingNumber).ToList();
    }

    /// <summary>
    /// Moves the event's live room bookings by the same amount as the event (F-38), refusing the whole
    /// move — before anything is written — when a room is taken at the new time.
    /// </summary>
    /// <remarks>
    /// A booking in a room that needs approval waits for approval again once moved, as the event does.
    /// The room's own limits (longest booking, furthest ahead) are lane 3's to apply to a moved booking.
    /// </remarks>
    private async Task<List<string>> MoveLinkedBookingsAsync(CompanyEvent e, TimeSpan delta, CancellationToken cancellationToken)
    {
        if (delta == TimeSpan.Zero) return new();

        var linked = await LiveLinkedBookings(e).Include(b => b.Room).ToListAsync(cancellationToken);
        if (linked.Count == 0) return new();

        var moving = linked.Select(b => b.Id).ToList();
        var taken = new List<string>();
        foreach (var b in linked)
        {
            var start = b.StartDateTime + delta;
            var end = b.EndDateTime + delta;
            var clash = await _unitOfWork.Repository<RoomBooking>().GetQueryable()
                .Where(o => o.TenantId == e.TenantId && o.RoomId == b.RoomId && !o.IsDeleted && !o.IsCancelled
                            && o.Status != BookingStatus.Cancelled && !moving.Contains(o.Id)
                            && o.StartDateTime < end && o.EndDateTime > start)
                .Select(o => o.BookingNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (clash != null) taken.Add($"{b.Room?.RoomName ?? "the room"} ({clash})");
        }

        if (taken.Count > 0)
            throw new InvalidOperationException(
                $"{e.EventName} cannot move: its room is already booked at the new time — {string.Join(", ", taken)}. "
              + "Move or cancel that booking, or choose another time.");

        foreach (var b in linked)
        {
            b.StartDateTime += delta;
            b.EndDateTime += delta;
            b.BookingDate = b.StartDateTime.Date;
            if (b.Room is { RequiresApproval: true } && b.ApprovalDate != null)
            {
                b.ApprovedById = null;
                b.ApprovedBy = null;
                b.ApprovalDate = null;
                b.Status = BookingStatus.Tentative;
            }
        }
        return linked.Select(b => b.BookingNumber).ToList();
    }

    /// <summary>
    /// What every move does — Reschedule, and an edit that changes the window (F-37, F-38, C-7). The new
    /// window is already validated and on the event; <paramref name="before"/> is where it was.
    /// </summary>
    /// <remarks>
    /// <para>The rooms move first, so a taken room refuses the move before anything changes.</para>
    ///
    /// <para>⚠ <b>Answers go back to awaiting a reply.</b> "Yes" was an answer for the old time, so an
    /// accepted or tentative invitation is asked again — and the chase stamp is cleared so the sweep may
    /// chase it. A decline stands: the person said no to the event, not to the hour.</para>
    ///
    /// <para>An approved event that moves waits for approval again: the approval was for the old time.</para>
    /// </remarks>
    private async Task<CompanyEventChangeDto> MoveAsync(
        CompanyEvent e, EventWindow before, string reason, CancellationToken cancellationToken)
    {
        var change = new CompanyEventChangeDto
        {
            BookingsMoved = await MoveLinkedBookingsAsync(e, EventWindow.Of(e).Start - before.Start, cancellationToken),
        };

        // ⚠ `??=` keeps the FIRST original (round 4, D7; C-2): "when was this first meant to be?" has one answer.
        e.OriginalStartDate ??= before.StartDate;
        e.OriginalStartTime ??= before.StartTime;
        e.OriginalEndDate ??= before.EndDate;
        e.OriginalEndTime ??= before.EndTime;

        e.IsRescheduled = true;
        e.RescheduledDate = DateTime.UtcNow;
        e.RescheduleReason = reason;
        e.Status = EventStatus.Rescheduled;
        e.ReminderSentDate = null;
        e.RsvpReminderSentDate = null;

        if (e.RequiresApproval && e.ApprovalDate != null)
        {
            e.ApprovedById = null;
            e.ApprovedBy = null;
            e.ApprovalDate = null;
            change.ApprovalCleared = true;
        }

        var answered = await _participantRepository.GetQueryable()
            .Where(p => p.EventId == e.Id && p.TenantId == e.TenantId && !p.IsDeleted
                        && (p.InvitationStatus == InvitationStatus.Accepted || p.InvitationStatus == InvitationStatus.Tentative))
            .ToListAsync(cancellationToken);
        foreach (var p in answered)
        {
            p.InvitationStatus = InvitationStatus.Sent;
            p.ResponseDate = null;
        }
        change.AnswersReset = answered.Count;

        return change;
    }

    /// <summary>Tells everybody invited that the event moved, and from when (round 4, D6).</summary>
    private Task NotifyRescheduledAsync(CompanyEvent entity, CancellationToken cancellationToken)
    {
        var original = entity.OriginalStartDate is { } os
            ? os.ToString("dddd, d MMMM yyyy")
              + (entity.OriginalStartTime is { } ost && entity.OriginalEndTime is { } oet
                  ? $@", {ost:hh\:mm} – {oet:hh\:mm}"
                  : string.Empty)
            : null;

        return NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventRescheduled,
            tokens =>
            {
                tokens["OriginalWhen"] = original;
                tokens["RescheduleReason"] = entity.RescheduleReason;
                return tokens;
            },
            "event rescheduled", cancellationToken: cancellationToken);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2b — approval on the workflow engine (D-10)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sends an event that needs approval to the engine: at creation (an event has no draft), and again
    /// when an approved event moves (its approval was for the old time).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Submitting must never approve.</b> With no published definition the engine answers
    /// Approved; <see cref="HrWorkflowFallbackAuthority.SubmitAsync"/> turns that into Pending, so the
    /// event waits for the module's own approve tier instead.</para>
    ///
    /// <para>A start that fails leaves the event awaiting approval, with no instance: the decision then
    /// falls to the approve tier (<see cref="DecideAsync"/>), so the event is never stuck.</para>
    /// </remarks>
    private async Task StartApprovalAsync(CompanyEvent e, CancellationToken cancellationToken)
    {
        try
        {
            var (result, outcome) = await HrWorkflowFallbackAuthority.SubmitAsync(_workflow, WorkflowEntityType, e.Id);
            if (!result.ExecutionResult.Success)
            {
                _logger.LogWarning("Approval did not start for event {EventNumber}: {Message}",
                    e.EventNumber, result.ExecutionResult.Message);
                return;
            }

            _workflowAdapters.GetAdapter(WorkflowEntityType).ApplySubmitOutcome(e, outcome, null);
            await _eventRepository.UpdateAsync(e);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approval did not start for event {EventNumber}; it waits for the approve tier.", e.EventNumber);
        }
    }

    /// <summary>
    /// The decision, from the engine when the event has an approval under way, and from the approve tier
    /// (<c>HR.Company.Approve</c>) when it has none.
    /// </summary>
    /// <remarks>
    /// "None" covers an unconfigured tenant, an event that predates lane 2b, and a start that failed —
    /// with no instance <c>CanUserApproveAsync</c> answers false for everybody, and the event would be
    /// stuck for ever.
    /// </remarks>
    private async Task<WorkflowOutcome> DecideAsync(CompanyEvent e, string action, string? comments)
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("Sign in to decide on an event.");

        var description = (action == "Reject" ? "reject" : "approve") + " a company event";
        if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, e.Id))
            return await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
                _workflow, _currentUserProvider, WorkflowEntityType, e.Id, userId, action, comments,
                description, HrPermissions.ApproveCompany);

        HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(_currentUserProvider, description, HrPermissions.ApproveCompany);
        return action == "Reject" ? WorkflowOutcome.Rejected : WorkflowOutcome.Approved;
    }

    /// <summary>Withdraws an approval still under way, when the event is cancelled or deleted.</summary>
    private async Task CancelApprovalAsync(CompanyEvent e, string reason)
    {
        try
        {
            if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, e.Id))
                await _workflow.CancelWorkflowAsync(WorkflowEntityType, e.Id, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not withdraw the approval of event {EventNumber}.", e.EventNumber);
        }
    }

    /// <summary>
    /// The rules a decision answers to, whoever the engine names (lane 2a): the event needs approval and
    /// has none, is scheduled, rescheduled or postponed, and is not the decider's own.
    /// </summary>
    private static void EnsureDecidable(CompanyEvent entity, Guid deciderEmployeeId, string verb)
    {
        if (CompanyEventRules.IsClosed(entity))
            throw new InvalidOperationException(
                $"{entity.EventName} is {ClosedState(entity)}, so there is nothing to {verb}.");
        if (!entity.RequiresApproval)
            throw new InvalidOperationException($"{entity.EventName} does not need approval.");
        if (entity.ApprovalDate != null)
            throw new InvalidOperationException(
                $"{entity.EventName} is already approved"
              + (entity.ApprovedBy is { } by ? $", by {by.FullName}." : "."));
        if (entity.Status is not (EventStatus.Scheduled or EventStatus.Rescheduled or EventStatus.Postponed))
            throw new InvalidOperationException(
                $"{entity.EventName} is {entity.Status}; only a scheduled, rescheduled or postponed event waits for approval.");
        if (deciderEmployeeId == entity.OrganizerId)
            throw new InvalidOperationException(
                $"You organise {entity.EventName}, so someone else must {verb} it.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2c — who an event is for (D-16), and its intranet announcement
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The sentence to warn with when the event's audience reaches nobody (D-16): management on a tenant
    /// whose units have no heads and whose staff no line managers, or a unit with nobody in it.
    /// </summary>
    private async Task<string?> AudienceWarningAsync(CompanyEvent e, CancellationToken cancellationToken)
    {
        if (CompanyEventRules.AudienceRuleOf(e) is not { } rule || rule.TargetType == HrAudienceTargetType.AllEmployees)
            return null;
        if (await _audience.CountAsync([rule], cancellationToken) > 0) return null;
        return rule.TargetType == HrAudienceTargetType.Management
            ? "This event is for management, and nobody counts as management yet: no organisation unit has a head "
              + "and nobody is named as a line manager. Name them in the organisation set-up, or invite people directly."
            : "This event is for an organisation unit with no active staff in it or beneath it. Check the unit, or invite people directly.";
    }

    public async Task<EventAudiencePreviewDto> PreviewAudienceAsync(
        ParticipantScope scope, EventVisibility visibility, Guid? organizationUnitId, CancellationToken cancellationToken = default)
    {
        var draft = new CompanyEvent { Scope = scope, Visibility = visibility, OrganizationUnitId = organizationUnitId, ShowOnCompanyCalendar = true };
        string? unitName = null;
        if (organizationUnitId is { } unitId)
            unitName = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .Where(u => u.Id == unitId && u.TenantId == GetTenantId() && !u.IsDeleted)
                .Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);

        var rule = CompanyEventRules.AudienceRuleOf(draft);
        return new EventAudiencePreviewDto
        {
            Audience = CompanyEventRules.DescribeAudience(draft, unitName),
            GuestListOnly = rule is null,
            Reach = rule is null ? 0 : await _audience.CountAsync([rule], cancellationToken),
            Warning = await AudienceWarningAsync(draft, cancellationToken),
        };
    }

    /// <summary>The intranet announcement's words, from the event: when, where, why, who organises it, the reply-by date.</summary>
    /// <remarks>⚠ Never the meeting password: the announcement reaches the whole audience, guests or not.</remarks>
    private static (string Title, string Summary, string Body) WordEventAnnouncement(CompanyEvent e)
    {
        var where = e.LocationType == EventLocation.Virtual
            ? "online"
            : string.Join(", ", new[] { e.VenueName, e.SiteLocation?.Name }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var summary = $"{CompanyEventRules.Describe(EventWindow.Of(e))}"
                      + (string.IsNullOrWhiteSpace(where) ? "." : $", {where}.");

        var body = new System.Text.StringBuilder(summary);
        if (!string.IsNullOrWhiteSpace(e.Description)) body.Append(' ').Append(e.Description.Trim());
        if (e.Organizer is { } organiser) body.Append($" Organised by {organiser.FullName}.");
        if (e.LocationType is EventLocation.Virtual or EventLocation.Hybrid && !string.IsNullOrWhiteSpace(e.OnlineMeetingLink))
            body.Append($" Join online: {e.OnlineMeetingLink}.");
        if (e.RequiresRsvp && e.RsvpDeadline is { } deadline)
            body.Append($" Replies by {CompanyEventRules.Describe(deadline)}.");
        return (e.EventName, summary, body.ToString());
    }

    /// <summary>Why the event cannot be announced on the intranet now, or null when it can.</summary>
    private static string? AnnounceRefusal(CompanyEvent e, HrAudienceRule? rule, int reach)
    {
        if (!e.ShowOnIntranet) return "This event is not marked to show on the intranet. Switch that on first.";
        if (CompanyEventRules.IsClosed(e)) return $"{e.EventName} is {ClosedState(e)}, so there is nothing to announce.";
        if (e.EndDate.Date < DateTime.UtcNow.Date) return $"{e.EventName} is over, so there is nothing to announce.";
        if (CompanyEventRules.IsAwaitingApproval(e)) return $"{e.EventName} is still awaiting approval. Announce it once it is approved.";
        if (rule is null) return "This event is for its guests and organiser only — their invitations tell them. Widen its audience to announce it.";
        if (reach == 0) return "The event's audience reaches nobody, so there is nobody to tell.";
        return null;
    }

    public async Task<EventAnnouncementPreviewDto> PreviewAnnouncementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id, cancellationToken);
        var rule = CompanyEventRules.AudienceRuleOf(entity);
        var reach = rule is null ? 0 : await _audience.CountAsync([rule], cancellationToken);
        var (title, summary, body) = WordEventAnnouncement(entity);
        var refusal = AnnounceRefusal(entity, rule, reach);
        return new EventAnnouncementPreviewDto
        {
            EventId = entity.Id, StaffReached = reach, CanAnnounce = refusal is null, Reason = refusal,
            Title = title, Summary = summary, Body = body,
        };
    }

    /// <remarks>
    /// <para><b>On HR's click, never on save</b> — the rule closures follow (L1-1): an announcement reaches
    /// everyone at once and cannot be unsent, and an event is often saved, corrected, then confirmed.
    /// "Show on intranet" marks the event as one to announce; this sends it.</para>
    ///
    /// <para>Addressed by the event's own audience rule — the same people the company calendar and the
    /// diaries treat as its audience — and shown until the day after it ends. The checks come first, so a
    /// refusal leaves no draft behind.</para>
    /// </remarks>
    public async Task<HrAnnouncementDto> AnnounceAsync(Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id, cancellationToken);
        var rule = CompanyEventRules.AudienceRuleOf(entity);
        var reach = rule is null ? 0 : await _audience.CountAsync([rule], cancellationToken);
        if (AnnounceRefusal(entity, rule, reach) is { } refusal) throw new InvalidOperationException(refusal);

        var (title, summary, body) = WordEventAnnouncement(entity);
        var draft = await _announcements.CreateAsync(new CreateHrAnnouncementDto
        {
            Title = title.Length > 200 ? title[..200] : title,
            Summary = summary,
            Body = body,
            Category = HrAnnouncementCategory.Event,
            EffectiveFrom = DateTime.UtcNow,
            // Shown until the event is over.
            ExpiresOn = DateTime.SpecifyKind(entity.EndDate.Date.AddDays(1), DateTimeKind.Utc),
            Audiences = [new HrAnnouncementAudienceDto { TargetType = rule!.TargetType, TargetId = rule.TargetId }],
        }, cancellationToken);

        _logger.LogInformation("Company event {EventNumber} announced on the intranet to {Reach} staff", entity.EventNumber, reach);
        return await _announcements.PublishAsync(draft.Id, publisherEmployeeId, cancellationToken);
    }

    public async Task<CompanyEventDto> CreateAsync(CreateCompanyEventDto createDto, Guid callerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        Refuse(CompanyEventRules.RefuseCategory(createDto.Category));

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        // D-11: the organiser is chosen, defaulting to whoever creates it — who is recorded as the creator.
        entity.OrganizerId = createDto.OrganizerId is { } chosen && chosen != Guid.Empty ? chosen : callerEmployeeId;
        await ValidateAsync(entity, EventWindow.Of(entity), tenantId, checkOrganiser: true, cancellationToken);

        entity.EventNumber = await _eventRepository.GetNextEventNumberAsync(tenantId, cancellationToken);
        entity.Status = EventStatus.Scheduled;
        StampCreator(entity);

        await _eventRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event created: {EventNumber}", entity.EventNumber);

        // D-10: an event that needs approval goes to the engine now — events have no draft to submit.
        if (entity.RequiresApproval) await StartApprovalAsync(entity, cancellationToken);

        // ⚠ Re-read before mapping. `entity` is the graph we just inserted: its Organizer,
        // Department and SiteLocation navigations are still null, so mapping it straight to a DTO
        // answers organizerName "" and locationName null. The caller cannot tell that from real
        // missing data, and any screen that renders the create response shows blanks.
        var created = await GetByIdAsync(entity.Id, cancellationToken);
        // D-16: an audience that reaches nobody is said, not refused — the guests can still be invited.
        if (await AudienceWarningAsync(entity, cancellationToken) is { } warning) created.Warnings.Add(warning);
        return created;
    }

    public async Task<CompanyEventDto> UpdateAsync(UpdateCompanyEventDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedEventAsync(updateDto.Id, cancellationToken);

        if (CompanyEventRules.IsClosed(entity))
            throw new InvalidOperationException(
                $"{entity.EventName} is {ClosedState(entity)}, so it can no longer be edited.");
        if (updateDto.Category != entity.Category)
            Refuse(CompanyEventRules.RefuseCategory(updateDto.Category));

        var before = EventWindow.Of(entity);
        var requested = new EventWindow(
            updateDto.StartDate, updateDto.StartTime, updateDto.EndDate, updateDto.EndTime, updateDto.IsAllDayEvent);
        var moving = !requested.SameAs(before);
        var rsvpDeadlineBefore = entity.RsvpDeadline;
        var organiserChanged = updateDto.OrganizerId is { } chosen && chosen != Guid.Empty && chosen != entity.OrganizerId;

        if (moving && string.IsNullOrWhiteSpace(updateDto.RescheduleReason))
            throw new InvalidOperationException(
                "Changing the dates or times moves the event, and everybody invited is told why. Give the reason for the change.");

        updateDto.UpdateEntity(entity);
        if (organiserChanged) entity.OrganizerId = updateDto.OrganizerId!.Value;
        if (updateDto.Status is { } status && status != entity.Status) ApplyStatus(entity, status);

        await ValidateAsync(entity, moving ? requested : before, tenantId, organiserChanged, cancellationToken);

        // ⚠ An edit that changes the window is a reschedule (F-37, R4-7.1): it used to write the dates
        // straight in, so nothing kept the original, the status stayed, and nobody was told.
        CompanyEventChangeDto? moved = null;
        if (moving)
            moved = await MoveAsync(entity, before, updateDto.RescheduleReason!.Trim(), cancellationToken);
        else if (entity.RsvpDeadline != rsvpDeadlineBefore)
            // A new deadline is a new chase (round 4, lane N-b2).
            entity.RsvpReminderSentDate = null;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event updated: {EventNumber}{Moved}", entity.EventNumber, moving ? " (rescheduled)" : string.Empty);

        if (moving) await NotifyRescheduledAsync(entity, cancellationToken);
        // D-10: an approved event that moved is approved afresh — its approval was for the old time.
        if (moved?.ApprovalCleared == true) await StartApprovalAsync(entity, cancellationToken);

        // ⚠ Re-read (F-46): the entity's navigations were loaded before the change, so a new organiser,
        // site or unit would answer with the old name.
        var updated = await GetByIdAsync(entity.Id, cancellationToken);
        if (await AudienceWarningAsync(entity, cancellationToken) is { } warning) updated.Warnings.Add(warning);
        return updated;
    }

    /// <remarks>
    /// <para>The record's rules first (lane 2a, <see cref="EnsureDecidable"/>), so a refusal explains
    /// itself in terms of the event — then the decision, through the engine (lane 2b, D-10). A definition
    /// with more than one stage leaves the event awaiting approval until the last.</para>
    ///
    /// <para>⚠ Approving from the generic <c>/workflow/inbox</c> drives the engine only and does not reach
    /// the event (cross-module #15), as for every HR approval. The inbox row links here.</para>
    /// </remarks>
    public async Task<bool> ApproveEventAsync(Guid eventId, Guid approvedById, string? comments = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(eventId, cancellationToken);
        EnsureDecidable(entity, approvedById, "approve");

        var outcome = await DecideAsync(entity, "Approve", comments);
        // ⚠ The approver's EMPLOYEE id: ApprovedById is an Employee foreign key.
        _workflowAdapters.GetAdapter(WorkflowEntityType).ApplyApprovalOutcome(entity, outcome, approvedById);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event approval for {EventNumber}: {Outcome}", entity.EventNumber, outcome);

        return true;
    }

    /// <summary>
    /// Rejects an event awaiting approval: it is cancelled, with the reason, its room bookings with it,
    /// and everybody invited is told (lane 2b). An event has no other state for "not going ahead".
    /// </summary>
    public async Task<CompanyEventChangeDto> RejectEventAsync(Guid eventId, Guid rejectedById, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Say why the event is not approved — the organiser and everybody invited are told.");

        var entity = await GetOwnedEventAsync(eventId, cancellationToken);
        EnsureDecidable(entity, rejectedById, "reject");

        var outcome = await DecideAsync(entity, "Reject", reason.Trim());
        _workflowAdapters.GetAdapter(WorkflowEntityType).ApplyApprovalOutcome(entity, outcome, rejectedById, reason.Trim());

        var change = new CompanyEventChangeDto();
        if (outcome == WorkflowOutcome.Rejected)
            change.BookingsCancelled = await CancelLinkedBookingsAsync(
                entity, $"{entity.EventNumber} was not approved: {reason.Trim()}", cancellationToken);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event {EventNumber} rejection: {Outcome}, {Bookings} room booking(s) cancelled",
            entity.EventNumber, outcome, change.BookingsCancelled.Count);

        if (outcome == WorkflowOutcome.Rejected)
            await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventCancelled,
                tokens =>
                {
                    tokens["CancellationReason"] = entity.CancellationReason;
                    return tokens;
                },
                "event not approved", cancellationToken: cancellationToken);

        change.Event = await GetByIdAsync(entity.Id, cancellationToken);
        return change;
    }

    public async Task<CompanyEventChangeDto> CancelEventAsync(CancelEventDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(cancelDto.EventId, cancellationToken);

        // ⚠ A second cancel overwrote the reason and emailed everybody again.
        if (CompanyEventRules.IsClosed(entity))
            throw new InvalidOperationException($"{entity.EventName} is already {ClosedState(entity)}.");

        var reason = cancelDto.CancellationReason.Trim();
        entity.IsCancelled = true;
        entity.CancellationDate = DateTime.UtcNow;
        entity.CancellationReason = reason;
        entity.Status = EventStatus.Cancelled;
        var bookings = await CancelLinkedBookingsAsync(entity, $"{entity.EventNumber} was cancelled: {reason}", cancellationToken);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event cancelled: {EventNumber}, {Bookings} room booking(s) with it",
            entity.EventNumber, bookings.Count);

        // D-10: an approval still under way is withdrawn — there is nothing left to approve.
        await CancelApprovalAsync(entity, $"The event was cancelled: {reason}");

        await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventCancelled,
            tokens =>
            {
                tokens["CancellationReason"] = entity.CancellationReason;
                return tokens;
            },
            "event cancelled", cancellationToken: cancellationToken);

        return new CompanyEventChangeDto
        {
            Event = await GetByIdAsync(entity.Id, cancellationToken),
            BookingsCancelled = bookings,
        };
    }

    public async Task<CompanyEventChangeDto> RescheduleEventAsync(RescheduleEventDto rescheduleDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(rescheduleDto.EventId, cancellationToken);

        if (CompanyEventRules.IsClosed(entity))
            throw new InvalidOperationException(
                $"{entity.EventName} is {ClosedState(entity)}, so it cannot be moved.");

        var before = EventWindow.Of(entity);
        // A move that names no times keeps the event's hours: the dialog moves the day.
        var keepHours = rescheduleDto.NewStartTime is null && rescheduleDto.NewEndTime is null;
        var requested = new EventWindow(
            rescheduleDto.NewStartDate,
            keepHours ? entity.StartTime : rescheduleDto.NewStartTime,
            rescheduleDto.NewEndDate,
            keepHours ? entity.EndTime : rescheduleDto.NewEndTime,
            entity.IsAllDayEvent);
        if (requested.SameAs(before))
            throw new InvalidOperationException(
                $"{entity.EventName} is already on {CompanyEventRules.Describe(before)}. Choose a different time.");

        // ⚠ F-38: a deadline after the new start cannot be met. A new one may come with the move; the
        // window check refuses the move without it.
        if (rescheduleDto.NewRsvpDeadline is { } newDeadline) entity.RsvpDeadline = newDeadline;
        ValidateWindow(entity, requested);

        var change = await MoveAsync(entity, before, rescheduleDto.RescheduleReason.Trim(), cancellationToken);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Company event rescheduled: {EventNumber}; {Bookings} room booking(s) moved, {Answers} answer(s) reset",
            entity.EventNumber, change.BookingsMoved.Count, change.AnswersReset);

        // ⚠ Round 4, D6. Everybody invited is told, and told what it moved FROM — which is only
        // possible because C-2 keeps the original window.
        await NotifyRescheduledAsync(entity, cancellationToken);
        // D-10: an approved event that moved is approved afresh — its approval was for the old time.
        if (change.ApprovalCleared) await StartApprovalAsync(entity, cancellationToken);

        change.Event = await GetByIdAsync(entity.Id, cancellationToken);
        return change;
    }

    public async Task<bool> CompleteEventAsync(CompleteEventDto completeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(completeDto.EventId, cancellationToken);

        if (CompanyEventRules.IsClosed(entity))
            throw new InvalidOperationException($"{entity.EventName} is already {ClosedState(entity)}.");
        // ⚠ F-40: an outcome and an attendance for something that has not happened yet.
        var start = EventWindow.Of(entity).Start;
        if (start > DateTime.UtcNow)
            throw new InvalidOperationException(
                $"{entity.EventName} has not started yet — it starts {CompanyEventRules.Describe(start)}. "
              + "Complete it once it has taken place.");

        entity.Status = EventStatus.Completed;
        entity.ActualStartTime = completeDto.ActualStartTime;
        entity.ActualEndTime = completeDto.ActualEndTime;
        entity.ActualAttendance = completeDto.ActualAttendance;
        entity.OutcomeSummary = completeDto.OutcomeSummary;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event completed: {EventNumber}", entity.EventNumber);

        return true;
    }

    /// <remarks>Cancels the event's live room bookings first, as Cancel does (F-39).</remarks>
    public async Task<CompanyEventChangeDto> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEventAsync(id, cancellationToken);
        var bookings = await CancelLinkedBookingsAsync(entity, $"{entity.EventNumber} was deleted.", cancellationToken);

        await _eventRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await CancelApprovalAsync(entity, "The event was deleted.");

        _logger.LogInformation("Company event deleted: {Id}, {Bookings} room booking(s) cancelled with it", id, bookings.Count);

        return new CompanyEventChangeDto { BookingsCancelled = bookings };
    }

    #region Participant Operations

    /// <inheritdoc />
    public async Task<int> SendRsvpRemindersAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        if (ev.IsCancelled || ev.Status == EventStatus.Cancelled)
            throw new InvalidOperationException(
                $"{ev.EventName} has been cancelled, so there is nothing left to RSVP to.");

        return await ChaseRsvpsAsync(ev, DateTime.UtcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> SendEventRemindersAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        if (ev.IsCancelled || ev.Status == EventStatus.Cancelled)
            throw new InvalidOperationException(
                $"{ev.EventName} has been cancelled, so a reminder would be telling people to attend "
              + "something that is not happening.");

        return await RemindAsync(ev, DateTime.UtcNow, cancellationToken);
    }

    /// <summary>
    /// Chases everybody who has not answered, and stamps the event so it is not chased again — by the
    /// button or the sweep, whichever comes second (round 4, lane N-b2).
    /// </summary>
    private async Task<int> ChaseRsvpsAsync(CompanyEvent ev, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var sent = await NotifyParticipantsAsync(ev, CompanyScheduleEmailCatalog.Events.EventRsvpReminder,
            enrich: null, "RSVP reminder",
            // ⚠ NotSent as well as Sent. A participant added before the invitation send existed
            // carries NotSent and has genuinely never been asked — chasing them is the first time
            // anybody has told them, which is exactly who this is for.
            filter: p => p.InvitationStatus is InvitationStatus.Sent or InvitationStatus.NotSent,
            cancellationToken);

        ev.RsvpReminderSentDate = nowUtc;
        await _eventRepository.UpdateAsync(ev);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return sent;
    }

    /// <summary>
    /// Reminds every participant who has not declined, and stamps the event so it is not reminded
    /// again for this date — by the button or the sweep, whichever comes second (lane N-b2).
    /// </summary>
    private async Task<int> RemindAsync(CompanyEvent ev, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // ⚠ Declined participants are NOT reminded. They have said they are not coming; a reminder
        // is the system ignoring the answer it asked for.
        var daysUntil = (ev.StartDate.Date - nowUtc.Date).TotalDays;
        var sent = await NotifyParticipantsAsync(ev, CompanyScheduleEmailCatalog.Events.EventReminder,
            tokens =>
            {
                tokens["DaysUntil"] = daysUntil >= 1 ? ((int)daysUntil).ToString() : null;
                return tokens;
            },
            "event reminder",
            filter: p => p.InvitationStatus != InvitationStatus.Declined,
            cancellationToken);

        ev.ReminderSentDate = nowUtc;
        await _eventRepository.UpdateAsync(ev);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return sent;
    }

    /// <inheritdoc />
    public Task<CompanyScheduleReminderRunDto> RunDueRemindersNowAsync(CancellationToken cancellationToken = default)
        => SendDueRemindersAsync(GetTenantId(), DateTime.UtcNow, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>Which events.</b> Live ones only — scheduled, confirmed or rescheduled, not cancelled,
    /// not postponed (a postponed event has no date to be reminded of), and approved where approval is
    /// required: an event still waiting for its approval is not yet something to attend. The event's day
    /// must not have passed, and an RSVP deadline must still be ahead — a chase after it is too late to
    /// answer.</para>
    ///
    /// <para><b>When.</b> Day-granular, as the form asks: the reminder on or after the day that is
    /// <c>ReminderDaysBefore</c> days before the event; the chase on or after the day that is the lead
    /// before the deadline. A pass that finds one due sends it at once — a late pass catches up rather
    /// than skipping — and the sent-date makes every later pass leave it alone.</para>
    ///
    /// <para><b>One event at a time.</b> Each is stamped and saved as it is sent, so a failure halfway
    /// through a pass costs only what was not yet sent, and the next pass sends exactly that.</para>
    /// </remarks>
    public async Task<CompanyScheduleReminderRunDto> SendDueRemindersAsync(
        Guid tenantId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var lead = Math.Max(0, (await _policySettings.GetForTenantAsync(tenantId, cancellationToken)).CompanyEventRsvpChaseLeadDays);
        var today = nowUtc.Date;
        var run = new CompanyScheduleReminderRunDto { RsvpChaseLeadDays = lead };

        var candidates = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && !e.IsCancelled
                        && (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed || e.Status == EventStatus.Rescheduled)
                        && (!e.RequiresApproval || e.ApprovalDate != null)
                        && e.StartDate >= today
                        && ((e.SendReminders && e.ReminderDaysBefore != null && e.ReminderSentDate == null)
                            || (e.RequiresRsvp && e.RsvpDeadline != null && e.RsvpReminderSentDate == null && e.RsvpDeadline > nowUtc)))
            .OrderBy(e => e.StartDate)
            .ToListAsync(cancellationToken);

        foreach (var ev in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ev.SendReminders && ev.ReminderDaysBefore is { } daysBefore && ev.ReminderSentDate is null
                && ev.StartDate.Date.AddDays(-Math.Max(0, daysBefore)) <= today)
            {
                run.EmailsSent += await RemindAsync(ev, nowUtc, cancellationToken);
                run.Reminded.Add(ev.EventNumber);
            }

            if (ev.RequiresRsvp && ev.RsvpDeadline is { } deadline && ev.RsvpReminderSentDate is null
                && deadline > nowUtc && deadline.Date.AddDays(-lead) <= today)
            {
                run.EmailsSent += await ChaseRsvpsAsync(ev, nowUtc, cancellationToken);
                run.RsvpChased.Add(ev.EventNumber);
            }
        }

        if (run.Reminded.Count > 0 || run.RsvpChased.Count > 0)
            _logger.LogInformation(
                "Company schedule reminders for tenant {TenantId}: reminded {Reminded}, chased {Chased}, {Emails} email(s).",
                tenantId, run.Reminded.Count, run.RsvpChased.Count, run.EmailsSent);
        return run;
    }

    public async Task<EventParticipantDto> AddParticipantAsync(CreateEventParticipantDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        if (createDto.EmployeeId.HasValue)
        {
            var isAlreadyParticipant = await _participantRepository.IsParticipantAsync(createDto.EventId, createDto.EmployeeId.Value);
            if (isAlreadyParticipant)
                throw new InvalidOperationException("Employee is already a participant in this event");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.InvitationStatus = InvitationStatus.Sent;
        entity.InvitationSentDate = DateTime.UtcNow;

        await _participantRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _participantRepository.GetQueryable()
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == entity.Id && p.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Participant added to event: {EventId}", createDto.EventId);

        // ⚠ Round 4, D6. InvitationSentDate was stamped above long before anything was sent. Now it
        // is true. Best-effort: the participant row is already committed, and an unreachable mail
        // server must not undo somebody's place on the invitation list.
        var invitedTo = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .FirstOrDefaultAsync(e => e.Id == createDto.EventId && e.TenantId == tenantId, cancellationToken);
        if (invitedTo is not null)
        {
            var name = entity!.Employee is not null
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}".Trim()
                : entity.ExternalParticipantName ?? "Colleague";
            var tokens = EventTokens(invitedTo, name);
            tokens["IsRequired"] = entity.IsRequired ? "true" : null;
            tokens["SpecialRequirements"] = entity.SpecialRequirements;

            await SendEventEmailAsync(
                invitedTo.TenantId,
                CompanyScheduleEmailCatalog.Events.EventInvitation,
                entity.Employee?.EmailAddress ?? entity.ExternalParticipantEmail,
                tokens, "event invitation");
        }

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EventParticipantDto>> GetParticipantsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _participantRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> RespondToInvitationAsync(RespondToEventInvitationDto responseDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _participantRepository.GetByIdAsync(responseDto.ParticipantId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Participant not found");

        entity.InvitationStatus = responseDto.Response;
        entity.ResponseDate = DateTime.UtcNow;
        entity.ResponseComments = responseDto.ResponseComments;

        await _participantRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invitation response recorded: {ParticipantId}", responseDto.ParticipantId);

        return true;
    }

    public async Task<bool> RemoveParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _participantRepository.GetByIdAsync(participantId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Participant not found");

        await _participantRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Participant removed: {ParticipantId}", participantId);

        return true;
    }

    #endregion

    #region Attendance Operations

    public async Task<EventAttendanceDto> MarkAttendanceAsync(MarkEventAttendanceDto markDto, Guid markedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(markDto.EventId, cancellationToken);

        var existingAttendance = await _attendanceRepository.GetByEventAndEmployeeAsync(markDto.EventId, markDto.EmployeeId);

        if (existingAttendance != null)
        {
            if (existingAttendance.TenantId != tenantId)
                throw new ArgumentException("Attendance record not found");

            existingAttendance.Attended = markDto.Attended;
            existingAttendance.CheckInTime = markDto.CheckInTime ?? DateTime.UtcNow;
            existingAttendance.AbsenceReason = markDto.AbsenceReason;
            existingAttendance.Notes = markDto.Notes;
            existingAttendance.MarkedById = markedById;

            await _attendanceRepository.UpdateAsync(existingAttendance);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return existingAttendance.ToDto();
        }

        var entity = markDto.ToEntity();
        entity.TenantId = tenantId;
        entity.MarkedById = markedById;
        entity.CheckInTime = markDto.CheckInTime ?? (markDto.Attended ? DateTime.UtcNow : null);

        await _attendanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _attendanceRepository.GetQueryable()
            .Include(a => a.Employee)
            .Include(a => a.MarkedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id && a.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Attendance marked for event: {EventId}, Employee: {EmployeeId}", markDto.EventId, markDto.EmployeeId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EventAttendanceDto>> GetAttendanceAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _attendanceRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> CheckOutAsync(CheckOutEventDto checkOutDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attendanceRepository.GetByIdAsync(checkOutDto.AttendanceId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Attendance record not found");

        entity.CheckOutTime = DateTime.UtcNow;
        entity.Notes = checkOutDto.Notes ?? entity.Notes;

        await _attendanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-out recorded: {AttendanceId}", checkOutDto.AttendanceId);

        return true;
    }

    #endregion

    #region Attachment Operations

    public async Task<EventAttachmentDto> AddAttachmentAsync(CreateEventAttachmentDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.UploadDate = DateTime.UtcNow;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added to event: {EventId}", createDto.EventId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EventAttachmentDto>> GetAttachmentsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _attachmentRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetByIdAsync(attachmentId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Attachment not found");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment deleted: {AttachmentId}", attachmentId);

        return true;
    }

    #endregion

    #region Task Operations

    public async Task<EventTaskDto> AddTaskAsync(CreateEventTaskDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.Status = EventTaskStatus.NotStarted;

        await _taskRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _taskRepository.GetQueryable()
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == entity.Id && t.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Task added to event: {EventId}", createDto.EventId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EventTaskDto>> GetTasksAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var entities = (await _taskRepository.GetByEventIdAsync(eventId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<EventTaskDto> UpdateTaskAsync(UpdateEventTaskDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _taskRepository.GetQueryable()
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == updateDto.Id && t.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Task not found");

        updateDto.UpdateEntity(entity);

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task updated: {TaskId}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> CompleteTaskAsync(CompleteEventTaskDto completeDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _taskRepository.GetByIdAsync(completeDto.TaskId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Task not found");

        entity.Status = EventTaskStatus.Completed;
        entity.CompletionDate = DateTime.UtcNow;
        entity.CompletionNotes = completeDto.CompletionNotes;

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task completed: {TaskId}", completeDto.TaskId);

        return true;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _taskRepository.GetByIdAsync(taskId);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException("Task not found");

        await _taskRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task deleted: {TaskId}", taskId);

        return true;
    }

    #endregion

    #region Helper Methods

    // ⚠ Round 4, D7 (C-6). The count-based generator that used to live here issued
    // `EVT-{year}-{COUNT(*) + 1}` over LIVE rows. Soft-deleted rows are excluded from that count, so
    // deleting an event freed its number and the next create took it — and the index was not unique,
    // so nothing complained and two events quietly shared a reference. Number issuing now goes
    // through the shared sequence in the repository, which probes with IgnoreQueryFilters so a
    // deleted row still holds its number. See ICompanyEventRepository.GetNextEventNumberAsync.

    #endregion
}

#endregion Company Event Service

#region Meeting Room Service

public class MeetingRoomService : IMeetingRoomService
{
    private readonly IMeetingRoomRepository _roomRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MeetingRoomService> _logger;

    public MeetingRoomService(
        IMeetingRoomRepository roomRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<MeetingRoomService> logger)
    {
        _roomRepository = roomRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<MeetingRoom> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _roomRepository.GetQueryable()
            .Include(r => r.SiteLocation)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Meeting room with ID '{id}' not found.");
        return entity;
    }

    public async Task<MeetingRoomDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MeetingRoomDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _roomRepository.GetQueryable()
            .Include(r => r.SiteLocation)
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<MeetingRoomDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _roomRepository.GetQueryable()
            .Include(r => r.SiteLocation)
            .Where(r => r.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.RoomName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MeetingRoomDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<MeetingRoomSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _roomRepository.GetByLocationAsync(locationId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MeetingRoomSummaryDto>> GetAvailableRoomsAsync(DateTime startDateTime, DateTime endDateTime, int? minCapacity = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _roomRepository.GetAvailableRoomsAsync(startDateTime, endDateTime, minCapacity))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MeetingRoomSummaryDto>> GetActiveRoomsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _roomRepository.GetActiveRoomsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<MeetingRoomDto> CreateAsync(CreateMeetingRoomDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await EnsureLocationExistsAsync(tenantId, entity.LocationId, cancellationToken);

        if (string.IsNullOrEmpty(entity.RoomCode))
        {
            entity.RoomCode = await _roomRepository.GetNextRoomCodeAsync(tenantId, cancellationToken);
        }
        else
        {
            var codeExists = await _roomRepository.GetQueryable()
                .AnyAsync(r => r.TenantId == tenantId && r.RoomCode == entity.RoomCode, cancellationToken);
            if (codeExists)
                throw new InvalidOperationException($"Room code '{entity.RoomCode}' already exists for this tenant.");
        }

        await _roomRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Meeting room created: {RoomCode}", entity.RoomCode);

        // Re-read so locationName is resolved — see the note on CompanyEventService.CreateAsync.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<MeetingRoomDto> UpdateAsync(UpdateMeetingRoomDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _roomRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Meeting room updated: {RoomCode}", entity.RoomCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _roomRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Meeting room deleted: {Id}", id);

        return true;
    }

    /// <summary>
    /// Refuses a site that is not a live Location in this tenant.
    /// </summary>
    /// <remarks>
    /// Without this the bad id reaches SaveChanges and comes back as an unhandled FK violation —
    /// a 500 reading "Something went wrong while processing your request", which tells the user
    /// nothing and looks like an outage rather than a bad selection. Caught by the harness on the
    /// first run. The tenant check matters as much as the existence check: another tenant's
    /// location exists, and must still be refused here.
    /// </remarks>
    private async Task EnsureLocationExistsAsync(
        Guid tenantId, Guid locationId, CancellationToken cancellationToken)
    {
        var exists = await _unitOfWork.Repository<Location>().GetQueryable()
            .AnyAsync(l => l.Id == locationId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken);

        if (!exists)
            throw new InvalidOperationException(
                $"Site '{locationId}' is not a location in this organisation, so a room cannot be filed against it.");
    }

    // ⚠ Round 4, D7 (C-6) — see the note where GenerateEventNumberAsync used to be.
}

#endregion Meeting Room Service

#region Room Booking Service

public class RoomBookingService : IRoomBookingService
{
    private readonly IRoomBookingRepository _bookingRepository;
    private readonly IMeetingRoomRepository _roomRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RoomBookingService> _logger;

    public RoomBookingService(
        IRoomBookingRepository bookingRepository,
        IMeetingRoomRepository roomRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<RoomBookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _roomRepository = roomRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<RoomBooking> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Include(b => b.ApprovedBy)
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Room booking with ID '{id}' not found.");
        return entity;
    }

    private async Task<bool> HasConflictingBookingAsync(Guid tenantId, Guid roomId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var query = _bookingRepository.GetQueryable()
            .Where(b => b.TenantId == tenantId &&
                        b.RoomId == roomId &&
                        !b.IsCancelled &&
                        b.Status != BookingStatus.Cancelled &&
                        b.StartDateTime < endDateTime &&
                        b.EndDateTime > startDateTime);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<RoomBookingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<RoomBookingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<RoomBookingDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .Where(b => b.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.StartDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RoomBookingDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByRoomIdAsync(roomId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByBookerAsync(Guid bookedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByBookerAsync(bookedById))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByDateRangeAsync(startDate, endDate))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetByStatusAsync(BookingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<RoomBookingSummaryDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _bookingRepository.GetPendingApprovalsAsync())
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<RoomBookingDto> CreateAsync(CreateRoomBookingDto createDto, Guid bookedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var room = await _roomRepository.GetByIdAsync(createDto.RoomId);
        if (room == null || room.TenantId != tenantId)
            throw new ArgumentException("Meeting room not found");

        if (!room.IsBookable)
            throw new InvalidOperationException("This room is not available for booking");

        EnforceRoomRules(room, createDto.StartDateTime, createDto.EndDateTime, createDto.ExpectedAttendees);

        var hasConflict = await HasConflictingBookingAsync(
            tenantId, createDto.RoomId, createDto.StartDateTime, createDto.EndDateTime, cancellationToken: cancellationToken);

        if (hasConflict)
            throw new InvalidOperationException("There is a conflicting booking for this time slot");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.BookedById = bookedById;
        entity.BookingDate = DateTime.UtcNow;
        entity.BookingNumber = await _bookingRepository.GetNextBookingNumberAsync(tenantId, cancellationToken);
        entity.Status = room.RequiresApproval ? BookingStatus.Tentative : BookingStatus.Confirmed;

        await _bookingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking created: {BookingNumber}", entity.BookingNumber);

        // Re-read so roomName and bookedByName are resolved — see the note on
        // CompanyEventService.CreateAsync.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<RoomBookingDto> UpdateAsync(UpdateRoomBookingDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _bookingRepository.GetQueryable()
            .Include(b => b.Room)
            .Include(b => b.BookedBy)
            .FirstOrDefaultAsync(b => b.Id == updateDto.Id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Room booking with ID '{updateDto.Id}' not found.");

        // ⚠ The EDIT enforces them too. A rule checked only on create is a rule anyone can get
        // round by booking something legal and then changing it.
        var roomForRules = entity.Room ?? await _roomRepository.GetByIdAsync(entity.RoomId);
        if (roomForRules is not null)
            EnforceRoomRules(roomForRules, updateDto.StartDateTime, updateDto.EndDateTime, updateDto.ExpectedAttendees);

        var hasConflict = await HasConflictingBookingAsync(
            tenantId, entity.RoomId, updateDto.StartDateTime, updateDto.EndDateTime, updateDto.Id, cancellationToken);

        if (hasConflict)
            throw new InvalidOperationException("There is a conflicting booking for this time slot");

        updateDto.UpdateEntity(entity);

        await _bookingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking updated: {BookingNumber}", entity.BookingNumber);

        return entity.ToDto();
    }

    public async Task<bool> ApproveBookingAsync(Guid bookingId, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(bookingId, cancellationToken);

        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.Status = BookingStatus.Confirmed;

        await _bookingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking approved: {BookingNumber}", entity.BookingNumber);

        return true;
    }

    public async Task<bool> CancelBookingAsync(CancelRoomBookingDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(cancelDto.BookingId, cancellationToken);

        entity.IsCancelled = true;
        entity.CancellationDate = DateTime.UtcNow;
        entity.CancellationReason = cancelDto.CancellationReason;
        entity.Status = BookingStatus.Cancelled;

        await _bookingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking cancelled: {BookingNumber}", entity.BookingNumber);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _bookingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Room booking deleted: {Id}", id);

        return true;
    }


    /// <summary>
    /// The rules a room records and, until round 4 D7 (C-4), never enforced.
    /// </summary>
    /// <remarks>
    /// <para>&#9888; <c>MaxBookingDurationHours</c>, <c>AdvanceBookingDays</c> and <c>Capacity</c>
    /// were all settable on the room's admin screen and read by nothing. A room could be configured
    /// "2 hours maximum, 14 days ahead, seats 8" and then booked for a day, a year out, for forty
    /// people, with no complaint — which is worse than not having the fields, because somebody set
    /// them believing they meant something.</para>
    ///
    /// <para>Each rule is skipped when the room leaves it unset: null means "no limit", not zero.</para>
    /// </remarks>
    private static void EnforceRoomRules(MeetingRoom room, DateTime start, DateTime end, int expectedAttendees)
    {
        if (end <= start)
            throw new InvalidOperationException("A booking must end after it starts.");

        if (room.MaxBookingDurationHours is { } maxHours && maxHours > 0)
        {
            var hours = (end - start).TotalHours;
            if (hours > maxHours)
                throw new InvalidOperationException(
                    $"{room.RoomName} may be booked for at most {maxHours} hour(s) at a time; this booking is "
                  + $"{hours:0.#}. Shorten it, or book a room without that limit.");
        }

        if (room.AdvanceBookingDays is { } maxAhead && maxAhead > 0)
        {
            var daysAhead = (start.Date - DateTime.UtcNow.Date).TotalDays;
            if (daysAhead > maxAhead)
                throw new InvalidOperationException(
                    $"{room.RoomName} can only be booked up to {maxAhead} day(s) ahead; this booking is "
                  + $"{daysAhead:0} day(s) out.");
        }

        // ⚠ Capacity is a refusal, not a warning. A room that seats 8 cannot hold 40, and a booking
        // that says it will is a meeting that arrives and finds nowhere to sit.
        if (room.Capacity > 0 && expectedAttendees > room.Capacity)
            throw new InvalidOperationException(
                $"{room.RoomName} seats {room.Capacity}; this booking expects {expectedAttendees}.");
    }
}

#endregion Room Booking Service

#region Company Milestone Service

public class CompanyMilestoneService : ICompanyMilestoneService
{
    private readonly ICompanyMilestoneRepository _milestoneRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanyMilestoneService> _logger;

    public CompanyMilestoneService(
        ICompanyMilestoneRepository milestoneRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompanyMilestoneService> logger)
    {
        _milestoneRepository = milestoneRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<CompanyMilestone> GetOwnedAsync(Guid id)
    {
        var entity = await _milestoneRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Company milestone with ID '{id}' not found.");
        return entity;
    }

    public async Task<CompanyMilestoneDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CompanyMilestoneDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _milestoneRepository.GetQueryable().Where(m => m.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.MilestoneDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyMilestoneDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetByCategoryAsync(MilestoneCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetByCategoryAsync(category))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetByDateRangeAsync(startDate, endDate))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CompanyMilestoneDto>> GetUpcomingMilestonesAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _milestoneRepository.GetUpcomingMilestonesAsync(daysAhead))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<CompanyMilestoneDto> CreateAsync(CreateCompanyMilestoneDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _milestoneRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company milestone created: {Title}", entity.Title);

        return entity.ToDto();
    }

    public async Task<CompanyMilestoneDto> UpdateAsync(UpdateCompanyMilestoneDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        updateDto.UpdateEntity(entity);

        await _milestoneRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company milestone updated: {Title}", entity.Title);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _milestoneRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company milestone deleted: {Id}", id);

        return true;
    }
}

#endregion Company Milestone Service

#region Business Closure Service

public class BusinessClosureService : IBusinessClosureService
{
    private readonly IBusinessClosureRepository _closureRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrClosureCalendar _closureCalendar;
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly IHrAudienceResolver _audience;
    private readonly ILeaveService _leaveService;
    private readonly IHrAnnouncementService _announcements;
    private readonly ILogger<BusinessClosureService> _logger;

    public BusinessClosureService(
        IBusinessClosureRepository closureRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IHrClosureCalendar closureCalendar,
        IHrWorkingDayCalculator workingDays,
        IHrAudienceResolver audience,
        ILeaveService leaveService,
        IHrAnnouncementService announcements,
        ILogger<BusinessClosureService> logger)
    {
        _closureRepository = closureRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _closureCalendar = closureCalendar;
        _workingDays = workingDays;
        _audience = audience;
        _leaveService = leaveService;
        _announcements = announcements;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// The tenant's closures with every name a DTO shows. ⚠ The tenant filter is in the query, not
    /// applied after loading every tenant's rows (F-30).
    /// </summary>
    private IQueryable<BusinessClosure> WithNames(Guid tenantId) => _closureRepository.GetQueryable()
        .Include(c => c.SiteLocation)
        .Include(c => c.Department)
        .Include(c => c.OrganizationUnit)
        .Include(c => c.AnnouncedBy)
        .Where(c => c.TenantId == tenantId);

    private async Task<BusinessClosure> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await WithNames(GetTenantId()).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Business closure with ID '{id}' not found.");
        return entity;
    }

    public async Task<BusinessClosureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<BusinessClosureDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await WithNames(GetTenantId()).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<BusinessClosureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = WithNames(GetTenantId());

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BusinessClosureDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Closures with a day in the range — overlapping it, not only contained in it (F-4), and a
    /// recurring closure whose yearly repeat falls in it (C-38).
    /// </summary>
    public async Task<IEnumerable<BusinessClosureDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => await InRangeAsync(DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate), cancellationToken);

    public async Task<IEnumerable<BusinessClosureDto>> GetByTypeAsync(ClosureType type, CancellationToken cancellationToken = default)
    {
        var entities = await WithNames(GetTenantId()).Where(c => c.Type == type).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    /// <summary>
    /// The closures that shut this site: the company-wide ones and the site's own, by each closure's
    /// type (D-1). A unit-wide closure is not listed — it follows the unit's staff, not a building.
    /// </summary>
    public async Task<IEnumerable<BusinessClosureDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var entities = await WithNames(GetTenantId()).OrderBy(c => c.StartDate).ToListAsync(cancellationToken);
        return entities
            .Where(c => BusinessClosureRules.ScopeOf(c) is var scope
                        && (scope.Kind == ClosureScopeKind.Company
                            || (scope.Kind == ClosureScopeKind.Site && scope.TargetId == locationId)))
            .ToDtoList();
    }

    /// <summary>Closures with a day from today (UTC) to <paramref name="daysAhead"/> days on, recurring ones included.</summary>
    public async Task<IEnumerable<BusinessClosureDto>> GetUpcomingClosuresAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await InRangeAsync(today, today.AddDays(Math.Clamp(daysAhead, 0, 366)), cancellationToken);
    }

    private async Task<List<BusinessClosureDto>> InRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) return [];

        var tenantId = GetTenantId();
        var candidates = await BusinessClosureRules
            .Candidates(WithNames(tenantId), tenantId, from, to)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(c => BusinessClosureRules.OccurrencesIn(c, from, to).Any())
            .OrderBy(c => c.StartDate)
            .ToDtoList();
    }

    /// <summary>
    /// Whether a closure that is a day off covers <paramref name="date"/> for someone at this site
    /// and in this unit (lane 1: C-37, F-2).
    /// </summary>
    /// <remarks>
    /// Compares DATES — a time on the closure's last day used to make that day answer false. With
    /// no site and no unit, only a company-wide closure answers yes. A partial closure keeps the day
    /// a working day, so it never answers yes.
    /// </remarks>
    public Task<bool> IsClosureDateAsync(DateTime date, Guid? locationId = null, Guid? organizationUnitId = null, CancellationToken cancellationToken = default)
        => _closureCalendar.IsNonWorkingClosureAsync(
            GetTenantId(), DateOnly.FromDateTime(date), locationId, organizationUnitId, cancellationToken);

    public async Task<BusinessClosureDto> CreateAsync(CreateBusinessClosureDto createDto, Guid announcedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.AnnouncedById = announcedById;
        entity.AnnouncementDate = DateTime.UtcNow;

        var warnings = await ValidateAsync(entity, tenantId, cancellationToken);

        await _closureRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business closure created: {Title}", entity.Title);

        // D-15a: leave already granted over these days is recounted. After the save, so the
        // recount sees the closure.
        var recharge = await _leaveService.RechargeForDaysOffChangeAsync(
            tenantId, SpansOf(entity), $"a business closure, {entity.Title}, was added", cancellationToken);

        // Re-read so the site, unit and announcer names are resolved — see the note on
        // CompanyEventService.CreateAsync.
        var dto = await GetByIdAsync(entity.Id, cancellationToken);
        dto.Warnings = warnings;
        dto.LeaveRecharge = recharge;
        return dto;
    }

    public async Task<BusinessClosureDto> UpdateAsync(UpdateBusinessClosureDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        // The days it covered BEFORE the change: a closure moved off a day gives that day back.
        var before = SpansOf(entity);

        updateDto.UpdateEntity(entity);
        var warnings = await ValidateAsync(entity, entity.TenantId, cancellationToken);

        await _closureRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business closure updated: {Title}", entity.Title);

        // D-15a: before and after. A change that moves no day off (a new title, a note) finds no
        // count changed, and nobody is told anything.
        var recharge = await _leaveService.RechargeForDaysOffChangeAsync(
            entity.TenantId, before.Concat(SpansOf(entity)).ToList(),
            $"a business closure, {entity.Title}, was changed", cancellationToken);

        // Re-read, as create does: the navigations still point at the old site and unit (F-46).
        var dto = await GetByIdAsync(entity.Id, cancellationToken);
        dto.Warnings = warnings;
        dto.LeaveRecharge = recharge;
        return dto;
    }

    /// <summary>Deletes the closure and recounts the leave it covered (D-15a).</summary>
    public async Task<LeaveRechargeResultDto> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        var covered = SpansOf(entity);

        await _closureRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Business closure deleted: {Id}", id);

        return await _leaveService.RechargeForDaysOffChangeAsync(
            entity.TenantId, covered, $"a business closure, {entity.Title}, was removed", cancellationToken);
    }

    /// <summary>
    /// The one-time pass (lane 1c): all granted leave in the current and later leave years recounted
    /// against the closures and holidays as they stand. For closures recorded before the recount
    /// existed, which never change and so never trigger it.
    /// </summary>
    public Task<LeaveRechargeResultDto> RechargeAllOpenLeaveAsync(bool dryRun = false, CancellationToken cancellationToken = default)
        => _leaveService.RechargeAllOpenLeaveAsync(GetTenantId(), dryRun, cancellationToken);

    public async Task<ClosureAnnouncementPreviewDto> PreviewAnnouncementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        var reach = (await _audience.ResolveForTenantAsync(
            entity.TenantId, [BusinessClosureRules.AudienceRuleOf(BusinessClosureRules.ScopeOf(entity))],
            cancellationToken)).Count;
        var (title, summary, body) = WordAnnouncement(entity);

        return new ClosureAnnouncementPreviewDto
        {
            ClosureId = entity.Id,
            StaffCovered = reach,
            CanAnnounce = reach > 0,
            Title = title,
            Summary = summary,
            Body = body,
        };
    }

    /// <remarks>
    /// <para><b>On HR's click, never on save</b> (L1-1). An announcement reaches every covered person
    /// at once and cannot be unsent, and a closure is often typed, corrected, then confirmed — publishing
    /// on save would make each correction another broadcast.</para>
    ///
    /// <para>The audience is the closure's own scope as an audience rule, so the announcement reaches
    /// exactly the people leave and the diaries treat as covered. Publishing raises the announcement's
    /// in-app topic, and email where the topic has it on. The checks come first, so a refusal leaves no
    /// draft behind.</para>
    /// </remarks>
    public async Task<HrAnnouncementDto> AnnounceAsync(Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        var occurrence = NextOccurrence(entity);
        if (occurrence.End < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("This closure is over, so there is nothing to announce.");

        var preview = await PreviewAnnouncementAsync(id, cancellationToken);
        if (!preview.CanAnnounce)
            throw new InvalidOperationException(
                "No active staff are covered by this closure, so there is nobody to tell. Check its site or unit.");

        var rule = BusinessClosureRules.AudienceRuleOf(BusinessClosureRules.ScopeOf(entity));
        var draft = await _announcements.CreateAsync(new CreateHrAnnouncementDto
        {
            Title = preview.Title,
            Summary = preview.Summary,
            Body = preview.Body,
            Category = HrAnnouncementCategory.General,
            EffectiveFrom = DateTime.UtcNow,
            // Shown until the closure is over.
            ExpiresOn = occurrence.End.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Audiences = [new HrAnnouncementAudienceDto { TargetType = rule.TargetType, TargetId = rule.TargetId }],
        }, cancellationToken);

        _logger.LogInformation("Business closure {Title} announced to {Reach} staff", entity.Title, preview.StaffCovered);
        return await _announcements.PublishAsync(draft.Id, publisherEmployeeId, cancellationToken);
    }

    public async Task<List<EmployeeClosureDaysDto>> GetEmployeeClosureDaysAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, bool includePartial,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
            throw new InvalidOperationException("Name at least one employee.");
        if (employeeIds.Count > 500)
            throw new InvalidOperationException("Ask for 500 employees or fewer at a time.");
        if (to < from)
            throw new InvalidOperationException("The range ends before it starts.");
        if (to.DayNumber - from.DayNumber > 366)
            throw new InvalidOperationException("Ask for a year or less at a time.");

        // Coverage reads this tenant's employees only, so an id from elsewhere answers no days.
        var days = await _closureCalendar.GetClosureDaysAsync(
            GetTenantId(), employeeIds, from, to, includePartial, cancellationToken);
        return days.Select(kv => new EmployeeClosureDaysDto
        {
            EmployeeId = kv.Key,
            Days = kv.Value.Select(d => new EmployeeClosureDayDto
            {
                Date = d.Date, ClosureId = d.ClosureId, Title = d.Title,
                IsPaid = d.IsPaid, IsWorkingDay = d.IsWorkingDay,
            }).ToList(),
        }).ToList();
    }

    /// <summary>The occurrence an announcement is about: the next one from today for a yearly closure.</summary>
    private static ClosureOccurrence NextOccurrence(BusinessClosure closure)
    {
        if (!closure.RecursAnnually) return BusinessClosureRules.FirstOccurrence(closure);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return BusinessClosureRules.OccurrencesIn(closure, today, today.AddDays(366))
            .Where(o => o.End >= today)
            .DefaultIfEmpty(BusinessClosureRules.FirstOccurrence(closure))
            .First();
    }

    /// <summary>
    /// The announcement's words, from the closure: who, when, why, whether it is paid and worked. A
    /// company fact — the reason is shared, nothing about any one person is.
    /// </summary>
    private static (string Title, string Summary, string Body) WordAnnouncement(BusinessClosure closure)
    {
        var when = BusinessClosureRules.Describe(NextOccurrence(closure));
        var who = BusinessClosureRules.ScopeOf(closure).Kind switch
        {
            ClosureScopeKind.Site => closure.SiteLocation?.Name ?? "The site",
            ClosureScopeKind.Unit => $"{closure.OrganizationUnit?.Name ?? "The unit"} and the units beneath it",
            _ => "The company",
        };
        var nonWorking = BusinessClosureRules.IsNonWorking(closure);

        var title = nonWorking ? $"Closure: {closure.Title}" : $"Reduced operations: {closure.Title}";
        var summary = nonWorking
            ? $"{who} will be closed on {when}."
            : $"{who} will run reduced operations on {when}.";

        var body = new System.Text.StringBuilder(summary);
        if (closure.RecursAnnually) body.Append(" The closure repeats every year on the same dates.");
        if (!string.IsNullOrWhiteSpace(closure.Reason)) body.Append($" Reason: {closure.Reason.Trim()}");
        if (nonWorking)
        {
            body.Append(closure.IsPaidClosure ? " Staff are paid for these days." : " These days are unpaid.");
            body.Append(" They are not working days, so any leave you have booked over them no longer counts them.");
        }
        else
        {
            body.Append(" They remain working days.");
        }

        return (title, summary, body.ToString());
    }

    /// <summary>
    /// The days a closure covers, for the recount: the closure itself, or for a yearly one each
    /// repeat from its first to two years past today — no granted leave reaches further.
    /// </summary>
    private static List<(DateOnly From, DateOnly To)> SpansOf(BusinessClosure closure)
    {
        var first = BusinessClosureRules.FirstOccurrence(closure);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = (first.End > today ? first.End : today).AddYears(2);
        return BusinessClosureRules.OccurrencesIn(closure, first.Start, until)
            .Select(o => (o.Start, o.End))
            .ToList();
    }

    /// <summary>
    /// The one validator for create and update (lane 1). Refuses by throwing, with a sentence that
    /// says what to change; answers the warnings that do not stop the save.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The type against the scope and dates, the two derived flags set (D-1, C-38) —
    /// <see cref="BusinessClosureRules.ValidateAndNormalise"/>.</item>
    /// <item>The site and the unit exist in this tenant: a stranger's id used to surface as a
    /// foreign-key 500.</item>
    /// <item>No second closure of the same scope on the same days, a yearly repeat included (C-39):
    /// the refusal names the other one, which is the one to change.</item>
    /// <item>Warned, not refused: a day that is already a public holiday, and a scope with nobody in
    /// it — a site high in the location tree has no staff assigned to it directly.</item>
    /// </list>
    /// </remarks>
    private async Task<List<string>> ValidateAsync(BusinessClosure entity, Guid tenantId, CancellationToken cancellationToken)
    {
        var refusal = BusinessClosureRules.ValidateAndNormalise(entity);
        if (refusal != null)
            throw new InvalidOperationException(refusal);

        string? siteName = null;
        if (entity.LocationId is { } locationId)
        {
            siteName = await _unitOfWork.Repository<Location>().GetQueryable()
                .Where(l => l.Id == locationId && l.TenantId == tenantId && !l.IsDeleted)
                .Select(l => l.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The site chosen for this closure was not found. Choose the site again.");
        }

        string? unitName = null;
        if (entity.OrganizationUnitId is { } unitId)
        {
            unitName = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .Where(u => u.Id == unitId && u.TenantId == tenantId && !u.IsDeleted)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The organisation unit chosen for this closure was not found. Choose the unit again.");
        }

        var scope = BusinessClosureRules.ScopeOf(entity);
        var scopeText = scope.Kind switch
        {
            ClosureScopeKind.Site => siteName!,
            ClosureScopeKind.Unit => $"{unitName} and everything beneath it",
            _ => "the whole company",
        };

        // ── C-39: one closure per scope per day ────────────────────────────────
        var others = await _closureRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.Id != entity.Id)
            .ToListAsync(cancellationToken);
        var clash = others.FirstOrDefault(o =>
            BusinessClosureRules.ScopeOf(o) == scope && BusinessClosureRules.Overlap(o, entity));
        if (clash != null)
        {
            var when = BusinessClosureRules.Describe(BusinessClosureRules.FirstOccurrence(clash))
                       + (clash.RecursAnnually ? ", every year" : string.Empty);
            throw new InvalidOperationException(
                $"'{clash.Title}' already closes {scopeText} on {when}. "
                + "Change that closure rather than adding a second one over the same days.");
        }

        // ── Warnings ───────────────────────────────────────────────────────────
        var warnings = new List<string>();

        var first = BusinessClosureRules.FirstOccurrence(entity);
        var holidays = await _workingDays.GetHolidaysAsync(tenantId, first.Start, first.End, cancellationToken);
        foreach (var holiday in holidays)
        {
            var day = BusinessClosureRules.Describe(new ClosureOccurrence(holiday.Date, holiday.Date));
            warnings.Add(holiday.InLieu
                ? $"{day} is already a day off in lieu of {holiday.Name}."
                : $"{day} is already a public holiday ({holiday.Name}).");
        }

        if (scope.Kind != ClosureScopeKind.Company)
        {
            var reach = (await _audience.ResolveForTenantAsync(
                tenantId, [BusinessClosureRules.AudienceRuleOf(scope)], cancellationToken)).Count;
            if (reach == 0)
                warnings.Add(scope.Kind == ClosureScopeKind.Site
                    ? $"No active employee is assigned to {siteName} itself, so this closure covers nobody. "
                      + "Staff are assigned to the sites beneath it; choose one of those."
                    : $"No active employee is in {unitName} or beneath it, so this closure covers nobody.");
        }

        return warnings;
    }
}

#endregion Business Closure Service

#region Fiscal Year Service

public class FiscalYearService : IFiscalYearService
{
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly IFiscalPeriodRepository _periodRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FiscalYearService> _logger;

    public FiscalYearService(
        IFiscalYearRepository fiscalYearRepository,
        IFiscalPeriodRepository periodRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<FiscalYearService> logger)
    {
        _fiscalYearRepository = fiscalYearRepository;
        _periodRepository = periodRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<FiscalYear> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fiscalYearRepository.GetQueryable()
            .Include(fy => fy.Periods)
            .FirstOrDefaultAsync(fy => fy.Id == id && fy.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Fiscal year with ID '{id}' not found.");
        return entity;
    }

    private async Task<FiscalPeriod> GetOwnedPeriodAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _periodRepository.GetQueryable()
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Fiscal period not found");
        return entity;
    }

    public async Task<FiscalYearDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDto();
    }

    public async Task<FiscalYearDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<FiscalYearDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _fiscalYearRepository.GetQueryable()
            .Include(fy => fy.Periods)
            .Where(fy => fy.TenantId == tenantId)
            .OrderByDescending(fy => fy.Year)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<FiscalYearDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _fiscalYearRepository.GetQueryable()
            .Include(fy => fy.Periods)
            .Where(fy => fy.TenantId == tenantId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(fy => fy.Year)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FiscalYearDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<FiscalYearDto?> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fiscalYearRepository.GetByYearAsync(year);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<FiscalYearDto?> GetCurrentFiscalYearAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _fiscalYearRepository.GetCurrentFiscalYearAsync();
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<FiscalYearDto>> GetByStatusAsync(FiscalYearStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _fiscalYearRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<FiscalYearDto> CreateAsync(CreateFiscalYearDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var existingYear = await _fiscalYearRepository.GetQueryable()
            .FirstOrDefaultAsync(fy => fy.TenantId == tenantId && fy.Year == createDto.Year, cancellationToken);
        if (existingYear != null)
            throw new InvalidOperationException($"Fiscal year {createDto.Year} already exists");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.Status = FiscalYearStatus.Active;

        if (createDto.IsCurrent)
        {
            await ClearCurrentFiscalYearAsync(tenantId, cancellationToken);
        }

        await _fiscalYearRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year created: {Year}", entity.Year);

        return entity.ToDto();
    }

    public async Task<FiscalYearDto> UpdateAsync(UpdateFiscalYearDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        if (updateDto.IsCurrent && !entity.IsCurrent)
        {
            await ClearCurrentFiscalYearAsync(tenantId, cancellationToken);
        }

        updateDto.UpdateEntity(entity);

        await _fiscalYearRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year updated: {Year}", entity.Year);

        return entity.ToDto();
    }

    public async Task<bool> SetAsCurrentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedAsync(id, cancellationToken);

        await ClearCurrentFiscalYearAsync(tenantId, cancellationToken);

        entity.IsCurrent = true;

        await _fiscalYearRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year set as current: {Year}", entity.Year);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        await _fiscalYearRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal year deleted: {Id}", id);

        return true;
    }

    #region Period Operations

    public async Task<FiscalPeriodDto> AddPeriodAsync(CreateFiscalPeriodDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync(createDto.FiscalYearId, cancellationToken);

        var existingPeriod = await _periodRepository.GetByPeriodNumberAsync(createDto.FiscalYearId, createDto.PeriodNumber);
        if (existingPeriod != null && existingPeriod.TenantId == tenantId)
            throw new InvalidOperationException($"Period number {createDto.PeriodNumber} already exists for this fiscal year");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _periodRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _periodRepository.GetQueryable()
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.Id == entity.Id && p.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Fiscal period added: {PeriodNumber}", createDto.PeriodNumber);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<FiscalPeriodDto>> GetPeriodsAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAsync(fiscalYearId, cancellationToken);
        var entities = (await _periodRepository.GetByFiscalYearIdAsync(fiscalYearId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToDtoList();
    }

    public async Task<FiscalPeriodDto> UpdatePeriodAsync(UpdateFiscalPeriodDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPeriodAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _periodRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal period updated: {PeriodNumber}", entity.PeriodNumber);

        return entity.ToDto();
    }

    public async Task<bool> ClosePeriodAsync(CloseFiscalPeriodDto closeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPeriodAsync(closeDto.PeriodId, cancellationToken);

        entity.IsClosed = true;
        entity.ClosedDate = DateTime.UtcNow;

        await _periodRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal period closed: {PeriodId}", closeDto.PeriodId);

        return true;
    }

    public async Task<bool> DeletePeriodAsync(Guid periodId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPeriodAsync(periodId, cancellationToken);

        await _periodRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fiscal period deleted: {PeriodId}", periodId);

        return true;
    }

    #endregion

    #region Helper Methods

    private async Task ClearCurrentFiscalYearAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currentYears = await _fiscalYearRepository.GetQueryable()
            .Where(fy => fy.TenantId == tenantId && fy.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var currentYear in currentYears)
        {
            currentYear.IsCurrent = false;
            await _fiscalYearRepository.UpdateAsync(currentYear);
        }
    }

    #endregion
}

#endregion Fiscal Year Service
