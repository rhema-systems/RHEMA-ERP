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
using Microsoft.Data.SqlClient;
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

    /// <summary>The in-app half of every notice (lane 2e-1); email stays on the catalogue.</summary>
    private readonly CompanyScheduleNotices _notices;

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
        IHrAnnouncementService announcements,
        CompanyScheduleNotices notices)
    {
        _notices = notices;
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
    /// <returns>
    /// Whether the mail server took the email within the wait (lane 2e-2, R4-6.3) — the email's own result, which the
    /// module used to ignore. False with no address, no mail server, a refusal or no answer in ten seconds.
    /// </returns>
    private async Task<bool> SendEventEmailAsync(
        Guid tenantId, string eventKey, string? toEmail, Dictionary<string, string?> tokens, string description,
        EmailAttachmentDto? calendarFile = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return false;

        try
        {
            // ⚠ By the EVENT'S tenant (round 4, lane N-b2): the reminder sweep sends with nobody signed
            // in, and without naming the tenant it would skip the tenant's own wording and print the
            // configuration's company name. ⚠ The tenant chooses the wording only: since master de8ad4fb2 the
            // mail server is looked up by the signed-in user's tenant, so the hourly sweep's emails find none
            // (cross-module #40) and are counted as not taken.
            var send = _templatedEmail.SendForTenantAsync(
                tenantId, CompanyScheduleEmailCatalog.Module, eventKey, toEmail, tokens,
                calendarFile is null ? null : new[] { calendarFile });

            if (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10))) == send)
                return await send;

            _logger.LogWarning(
                "{Description} email timed out after 10 s for {Email} — counted as not taken; the operation itself succeeded.",
                description, toEmail);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send the {Description} email to {Email} — the operation itself succeeded.",
                description, toEmail);
            return false;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2e-3 — the calendar file (D-14)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whom an event's calendar file names as organiser, so an outside guest's Accept or Decline reaches someone
    /// (F-36: HR records it at the desk): the organiser's own address, or else the mail server's sending address.
    /// </summary>
    private async Task<HrCalendarPerson?> CalendarOrganizerAsync(CompanyEvent ev, CancellationToken cancellationToken)
    {
        var organiser = ev.Organizer is { } loaded
            ? new { loaded.FirstName, loaded.LastName, loaded.EmailAddress }
            : await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .Where(x => x.Id == ev.OrganizerId && x.TenantId == ev.TenantId)
                .Select(x => new { x.FirstName, x.LastName, x.EmailAddress })
                .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(organiser?.EmailAddress))
            return new HrCalendarPerson(organiser.EmailAddress, $"{organiser.FirstName} {organiser.LastName}".Trim());

        var server = await _unitOfWork.Repository<ErpSystem.Core.Entities.EmailSettings>().GetQueryable().AsNoTracking()
            .Where(s => s.TenantId == ev.TenantId && !s.IsDeleted && s.FromAddress != "")
            .Select(s => new { s.FromAddress, s.FromName })
            .FirstOrDefaultAsync(cancellationToken);
        return server is null ? null : new HrCalendarPerson(server.FromAddress, server.FromName);
    }

    /// <summary>
    /// The calendar file one recipient's email carries (lane 2e-3, D-14): the event as their calendar should hold it,
    /// or its cancellation. The UID is the event's id, so every file replaces the last; the SEQUENCE is the event's,
    /// raised by each change before it is told.
    /// </summary>
    /// <remarks>
    /// Only the recipient is named as attendee — the guest list is nobody else's. An untimed event is an all-day one,
    /// as the emails treat it. The meeting password is never in it, as it is not in the emails.
    /// </remarks>
    private static EmailAttachmentDto CalendarFileFor(
        CompanyEvent ev, HrCalendarMethod method, HrCalendarPerson? organizer, string email, string? name, bool required)
    {
        var allDay = ev.IsAllDayEvent || ev.StartTime is null || ev.EndTime is null;
        var online = ev.LocationType is EventLocation.Virtual or EventLocation.Hybrid
                     && !string.IsNullOrWhiteSpace(ev.OnlineMeetingLink)
            ? ev.OnlineMeetingLink!.Trim()
            : null;
        var place = string.Join(", ", new[] { ev.VenueName, ev.VenueAddress, ev.SiteLocation?.Name }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        var location = ev.LocationType == EventLocation.Virtual ? online ?? "Online" : place.Length > 0 ? place : online;

        var description = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(ev.Description)) description.Append(ev.Description.Trim()).Append("\n\n");
        if (online is not null) description.Append("Join online: ").Append(online).Append('\n');
        if (ev.Organizer is { } organiser) description.Append("Organised by ").Append(organiser.FullName).Append('\n');
        description.Append("Reference ").Append(ev.EventNumber);

        return HrCalendarFile.Build(new HrCalendarEntry
        {
            Uid = $"company-event-{ev.Id:N}@rhema-erp",
            Sequence = ev.CalendarSequence,
            Method = method,
            Summary = ev.EventName,
            Description = description.ToString(),
            Location = location,
            Url = online,
            AllDay = allDay,
            FirstDay = DateOnly.FromDateTime(ev.StartDate),
            LastDay = DateOnly.FromDateTime(ev.EndDate),
            StartUtc = allDay ? ev.StartDate.Date : ev.StartDate.Date + ev.StartTime!.Value,
            EndUtc = allDay ? ev.EndDate.Date : ev.EndDate.Date + ev.EndTime!.Value,
            Organizer = organizer,
            Attendee = new HrCalendarPerson(email, name),
            AttendeeRequired = required,
            RsvpRequested = ev.RequiresRsvp,
        }, $"{ev.EventNumber}.ics");
    }

    /// <summary>Per tenant, per scope: the sweep walks every tenant in one.</summary>
    private readonly Dictionary<Guid, bool> _mailServerSetUp = new();

    /// <summary>
    /// Whether the tenant has a mail server set up — what explains a notice that emailed nobody (lane 2e-2).
    /// </summary>
    /// <remarks>
    /// Read untracked, by the tenant named — not through the sender's own lookup, which reads the signed-in user's
    /// tenant (#40) and loads the row it sends with.
    /// </remarks>
    private async Task<bool> MailServerSetUpAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_mailServerSetUp.TryGetValue(tenantId, out var known)) return known;
        var setUp = await _unitOfWork.Repository<ErpSystem.Core.Entities.EmailSettings>().GetQueryable().AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SmtpHost != "" && s.FromAddress != "", cancellationToken);
        _mailServerSetUp[tenantId] = setUp;
        return setUp;
    }

    /// <summary>
    /// Tells the event's guests something — a reschedule, a cancellation, a reminder — by email, and in the
    /// app those with a login (lane 2e-1).
    /// </summary>
    /// <remarks>
    /// <para>⚠ Reads the participants fresh rather than through the event's navigation, which callers may
    /// not have loaded. A notification loop over an empty unloaded collection tells nobody and looks
    /// like success.</para>
    ///
    /// <para><b>A change</b> (<paramref name="change"/>, the default) goes to the guests who were invited —
    /// not to one still waiting for the event's approval, who has never heard of it (F-33) — and never to
    /// whoever made it. <b>A reminder or a chase</b> goes to everyone it is for, the sender included: it is
    /// about the date, not an act.</para>
    ///
    /// <para><b>Counted (lane 2e-2, R4-6.3):</b> everyone it was for, and who it reached — by an email the mail
    /// server took, or a notice in the app. It used to count every guest with an address as sent.</para>
    /// </remarks>
    /// <param name="calendar">Lane 2e-3 (D-14): the calendar file each guest's email carries — the updated entry or its
    /// cancellation — or none (a reminder or a chase: the calendar already holds the event).</param>
    private async Task<CompanyEventNoticeResultDto> NotifyParticipantsAsync(
        CompanyEvent ev, string eventKey, Func<Dictionary<string, string?>, Dictionary<string, string?>>? enrich,
        string description, string inAppNotice, Func<EventParticipant, bool>? filter = null, bool change = true,
        IReadOnlyDictionary<string, object>? inAppData = null, HrCalendarMethod? calendar = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ev.TenantId;
        var actor = change ? await _notices.ActorEmployeeIdAsync(cancellationToken) : null;
        var participants = (await _participantRepository.GetQueryable()
                .Include(p => p.Employee)
                .Where(p => p.EventId == ev.Id && p.TenantId == tenantId && !p.IsDeleted)
                // ⚠ F-35 (lane 2d): a leaver is never invited, chased or reminded.
                .Where(p => p.EmployeeId == null || (p.Employee!.IsActive && !p.Employee!.IsDeleted))
                .ToListAsync(cancellationToken))
            .Where(p => filter is null || filter(p))
            .Where(p => !change || p.InvitationStatus != InvitationStatus.NotSent)
            .Where(p => actor is null || p.EmployeeId != actor)
            .ToList();

        var result = new CompanyEventNoticeResultDto
        {
            Issued = participants.Count,
            MailServerSetUp = await MailServerSetUpAsync(tenantId, cancellationToken),
        };
        var emailed = new HashSet<Guid>();
        var organizer = calendar is null ? null : await CalendarOrganizerAsync(ev, cancellationToken);
        foreach (var p in participants)
        {
            var name = p.Employee is not null
                ? $"{p.Employee.FirstName} {p.Employee.LastName}".Trim()
                : p.ExternalParticipantName ?? "Colleague";
            var email = p.Employee?.EmailAddress ?? p.ExternalParticipantEmail;
            if (string.IsNullOrWhiteSpace(email)) continue;

            var tokens = EventTokens(ev, name);
            if (enrich is not null) tokens = enrich(tokens);
            var file = calendar is { } method ? CalendarFileFor(ev, method, organizer, email, name, p.IsRequired) : null;

            if (await SendEventEmailAsync(ev.TenantId, eventKey, email, tokens, description, file))
            {
                emailed.Add(p.Id);
                result.Emailed++;
            }
            else result.EmailsNotTaken++;
        }

        var inApp = await _notices.TellAsync(ev, inAppNotice, CompanyScheduleNotices.ToGuest,
            participants.Where(p => p.EmployeeId != null).Select(p => p.EmployeeId!.Value),
            inAppData, actorToo: !change, cancellationToken);

        bool InApp(EventParticipant p) => p.EmployeeId is { } id && inApp.Contains(id);
        result.ToldInApp = participants.Count(InApp);
        result.Reached = participants.Count(p => emailed.Contains(p.Id) || InApp(p));

        _logger.LogInformation(
            "Company schedule: {Description} for {Issued} participant(s) of {EventNumber} reached {Reached} — {Emailed} email(s) taken, {NotTaken} not, {InApp} told in the app.",
            description, result.Issued, ev.EventNumber, result.Reached, result.Emailed, result.EmailsNotTaken, result.ToldInApp);
        return result;
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

    /// <summary>
    /// Tells the organiser (lane 2e-1, D-11) by email and in the app — unless the signed-in user is the organiser
    /// (bar <paramref name="actorToo"/>), the organiser has left, or <paramref name="alreadyTold"/> says their row on
    /// the guest list was told the same thing already.
    /// </summary>
    /// <returns>Whether it was for the organiser (issued 0 or 1), and whether it reached them (lane 2e-2).</returns>
    private async Task<CompanyEventNoticeResultDto> TellOrganiserAsync(
        CompanyEvent ev, string eventKey, string inAppNotice,
        Func<Dictionary<string, string?>, Dictionary<string, string?>>? enrich, string description,
        CancellationToken cancellationToken, bool actorToo = false, Func<InvitationStatus, bool>? alreadyTold = null)
    {
        var none = new CompanyEventNoticeResultDto();
        var organiser = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(x => x.Id == ev.OrganizerId && x.TenantId == ev.TenantId && !x.IsDeleted && x.IsActive)
            .Select(x => new { x.FirstName, x.LastName, x.EmailAddress })
            .FirstOrDefaultAsync(cancellationToken);
        if (organiser is null) return none;
        if (!actorToo && await _notices.ActorEmployeeIdAsync(cancellationToken) == ev.OrganizerId) return none;

        if (alreadyTold is not null)
        {
            var asGuest = await _participantRepository.GetQueryable()
                .Where(p => p.EventId == ev.Id && p.TenantId == ev.TenantId && !p.IsDeleted && p.EmployeeId == ev.OrganizerId)
                .Select(p => (InvitationStatus?)p.InvitationStatus)
                .FirstOrDefaultAsync(cancellationToken);
            if (asGuest is { } status && alreadyTold(status)) return none;
        }

        var result = new CompanyEventNoticeResultDto
        {
            Issued = 1,
            MailServerSetUp = await MailServerSetUpAsync(ev.TenantId, cancellationToken),
        };
        var emailed = false;
        if (!string.IsNullOrWhiteSpace(organiser.EmailAddress))
        {
            var tokens = EventTokens(ev, $"{organiser.FirstName} {organiser.LastName}".Trim());
            if (enrich is not null) tokens = enrich(tokens);
            emailed = await SendEventEmailAsync(ev.TenantId, eventKey, organiser.EmailAddress, tokens, description);
            if (emailed) result.Emailed = 1; else result.EmailsNotTaken = 1;
        }

        var inApp = (await _notices.TellAsync(ev, inAppNotice, CompanyScheduleNotices.ToOrganiser, [ev.OrganizerId],
            actorToo: actorToo, cancellationToken: cancellationToken)).Contains(ev.OrganizerId);
        result.ToldInApp = inApp ? 1 : 0;
        result.Reached = emailed || inApp ? 1 : 0;
        return result;
    }

    /// <summary>
    /// What a guest must hear of in an edit that keeps the time (lane 2e-1): the venue or the site, the joining
    /// link, or both — or null. A new time is a reschedule, told by its own notice.
    /// </summary>
    private async Task<(string What, string? SiteName)?> WhatChangedAsync(
        CompanyEvent e, string? venueBefore, string? linkBefore, Guid? siteBefore, CancellationToken cancellationToken)
    {
        var siteChanged = e.LocationId != siteBefore;
        var venue = siteChanged || !string.Equals(
            CompanyEventRules.Clean(venueBefore), CompanyEventRules.Clean(e.VenueName), StringComparison.Ordinal);
        var link = !string.Equals(
            CompanyEventRules.Clean(linkBefore), CompanyEventRules.Clean(e.OnlineMeetingLink), StringComparison.Ordinal);
        if (!venue && !link) return null;

        string? siteName = null;
        if (siteChanged && e.LocationId is { } site)
            siteName = await _unitOfWork.Repository<Location>().GetQueryable()
                .Where(l => l.Id == site && l.TenantId == e.TenantId)
                .Select(l => l.Name)
                .FirstOrDefaultAsync(cancellationToken);

        return (venue && link ? "The venue and the joining link" : venue ? "The venue" : "The joining link", siteName);
    }

    /// <summary>
    /// Sends the invitations not yet delivered: those that waited for the event's approval (F-33), and any that
    /// reached nobody (lane 2e-2) — each marked sent once it reached its guest.
    /// </summary>
    /// <remarks>
    /// ⚠ They were marked sent and saved BEFORE anything was sent, so an invitation no mail server took read "Sent"
    /// (R4-6.3). A guest who has since left is not invited (F-35) and is not counted.
    /// </remarks>
    private async Task<CompanyEventNoticeResultDto> InviteWaitingGuestsAsync(CompanyEvent ev, CancellationToken cancellationToken)
    {
        var waiting = await TenantGuests(ev.TenantId)
            .Where(p => p.EventId == ev.Id && p.InvitationStatus == InvitationStatus.NotSent)
            .Where(p => p.EmployeeId == null || (p.Employee!.IsActive && !p.Employee!.IsDeleted))
            .ToListAsync(cancellationToken);

        var result = new CompanyEventNoticeResultDto { MailServerSetUp = await MailServerSetUpAsync(ev.TenantId, cancellationToken) };
        foreach (var guest in waiting) await InviteAsync(ev, guest, result, cancellationToken);
        return result;
    }

    /// <summary>
    /// Sends one guest the invitation and marks it sent — only once it reached them (lane 2e-2, R4-6.3). Saved guest
    /// by guest, so a pass that dies half way leaves the rest to send again.
    /// </summary>
    /// <returns>Whether it reached them.</returns>
    private async Task<bool> InviteAsync(
        CompanyEvent ev, EventParticipant guest, CompanyEventNoticeResultDto tally, CancellationToken cancellationToken)
    {
        if (!await SendInvitationAsync(ev, guest, tally, cancellationToken)) return false;

        // An answer already given stands: a corrected address re-sends the invitation to someone who may have answered.
        if (guest.InvitationStatus == InvitationStatus.NotSent) guest.InvitationStatus = InvitationStatus.Sent;
        guest.InvitationSentDate = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
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

    /// <remarks>
    /// <para>Lane 2e-2: with the days the sweep sends the reminder and the chase, and whether a mail server is set up —
    /// so the page can say what is due and has reached nobody yet, and why no email went.</para>
    ///
    /// <para>⚠ <b>The event, then each collection on its own</b> (lane 2e-3). One query with all four collections had
    /// SQL Server sort their joined rows — each carrying a whole <c>Employee</c> — and on every fresh compile it asked
    /// for 387 MB of working memory, used none, and waited 29 s for the grant on UAT's 2 GB server: the event page
    /// answered 500 after 2e-3's migration recompiled it, and kept doing so, since a run that never finishes never
    /// teaches the server to ask for less. Each collection is now a seek on its event index, with no sort, and the
    /// context attaches them to the event.</para>
    /// </remarks>
    public async Task<CompanyEventDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
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

        await TenantGuests(tenantId).Where(p => p.EventId == id).LoadAsync(cancellationToken);
        await TenantRegister(tenantId).Where(a => a.EventId == id).LoadAsync(cancellationToken);
        await _attachmentRepository.GetQueryable()
            .Where(a => a.EventId == id && a.TenantId == tenantId && !a.IsDeleted)
            .LoadAsync(cancellationToken);
        await TenantTasks(tenantId).Where(t => t.EventId == id).LoadAsync(cancellationToken);

        var detail = entity.ToDetailDto();
        var lead = (await _policySettings.GetForTenantAsync(tenantId, cancellationToken)).CompanyEventRsvpChaseLeadDays;
        detail.ReminderDueOn = ReminderDueOn(entity);
        detail.RsvpChaseDueOn = RsvpChaseDueOn(entity, lead);
        detail.MailServerSetUp = await MailServerSetUpAsync(tenantId, cancellationToken);
        return detail;
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
        // Lane 2e-3 (D-14): the guests' calendar entries move with it.
        e.CalendarSequence++;

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
    private Task<CompanyEventNoticeResultDto> NotifyRescheduledAsync(CompanyEvent entity, CancellationToken cancellationToken)
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
            "event rescheduled", CompanyScheduleNotices.Rescheduled,
            calendar: HrCalendarMethod.Request, cancellationToken: cancellationToken);
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
        // Lane 2e-1: what the guests hear of — a postponement, or a new venue, site or joining link.
        var statusBefore = entity.Status;
        var venueBefore = entity.VenueName;
        var linkBefore = entity.OnlineMeetingLink;
        var siteBefore = entity.LocationId;

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

        // What the guests hear of, worked out before the save so the calendar's sequence rises with it (lane 2e-3,
        // D-14): a postponement takes the entry away, a new venue, site or link updates it. A move raises it in MoveAsync.
        var postponedNow = !moving && entity.Status == EventStatus.Postponed && statusBefore != EventStatus.Postponed;
        var changed = moving || postponedNow
            ? null
            : await WhatChangedAsync(entity, venueBefore, linkBefore, siteBefore, cancellationToken);
        if (postponedNow || changed is not null) entity.CalendarSequence++;

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event updated: {EventNumber}{Moved}", entity.EventNumber, moving ? " (rescheduled)" : string.Empty);

        // Lane 2e-2: who the edit's notice reached, for the save's answer.
        CompanyEventNoticeResultDto? told = null;
        if (moving)
            told = await NotifyRescheduledAsync(entity, cancellationToken);
        else if (postponedNow)
            // The user's ruling (2e-3): postponed has no date, so the calendar entry is taken away; the move to a
            // new date sends it again.
            told = await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventPostponed, null,
                "event postponed", CompanyScheduleNotices.Postponed,
                calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken);
        else if (changed is { } what)
            told = await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventChanged,
                tokens =>
                {
                    tokens["WhatChanged"] = what.What;
                    tokens["SiteName"] = what.SiteName;
                    return tokens;
                },
                "event changed", CompanyScheduleNotices.Changed,
                inAppData: new Dictionary<string, object> { ["What"] = what.What },
                // A postponed event's entries were taken away; a new venue must not put them back at the old date.
                calendar: entity.Status == EventStatus.Postponed ? null : HrCalendarMethod.Request,
                cancellationToken: cancellationToken);
        // D-10: an approved event that moved is approved afresh — its approval was for the old time.
        if (moved?.ApprovalCleared == true) await StartApprovalAsync(entity, cancellationToken);

        // ⚠ Re-read (F-46): the entity's navigations were loaded before the change, so a new organiser,
        // site or unit would answer with the old name.
        var updated = await GetByIdAsync(entity.Id, cancellationToken);
        updated.Told = told;
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

        // Lane 2e-1: approved at last — the invitations that waited go (F-33), and the organiser is told.
        if (outcome == WorkflowOutcome.Approved && entity.ApprovalDate != null)
        {
            var invited = await InviteWaitingGuestsAsync(entity, cancellationToken);
            await TellOrganiserAsync(entity, CompanyScheduleEmailCatalog.Events.EventApproved, CompanyScheduleNotices.Approved,
                tokens =>
                {
                    tokens["ApprovedBy"] = string.IsNullOrWhiteSpace(_currentUserProvider.FullName) ? null : _currentUserProvider.FullName;
                    // Lane 2e-2: the invitations that reached their guests, not the ones tried.
                    tokens["InvitationsSent"] = invited.Reached > 0 ? invited.Reached.ToString() : null;
                    return tokens;
                },
                "event approved", cancellationToken);
        }

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
        {
            change.BookingsCancelled = await CancelLinkedBookingsAsync(
                entity, $"{entity.EventNumber} was not approved: {reason.Trim()}", cancellationToken);
            // Lane 2e-3 (D-14): a guest who held an entry (invited before a move sent it back for approval) loses it.
            entity.CalendarSequence++;
        }

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event {EventNumber} rejection: {Outcome}, {Bookings} room booking(s) cancelled",
            entity.EventNumber, outcome, change.BookingsCancelled.Count);

        if (outcome == WorkflowOutcome.Rejected)
        {
            Dictionary<string, string?> WithReason(Dictionary<string, string?> tokens)
            {
                tokens["CancellationReason"] = entity.CancellationReason;
                return tokens;
            }

            change.Told = await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventCancelled, WithReason,
                "event not approved", CompanyScheduleNotices.Cancelled,
                calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken);
            // Lane 2e-1: the organiser hears why, unless their invitation already said it.
            change.Told.Add(await TellOrganiserAsync(entity, CompanyScheduleEmailCatalog.Events.EventCancelled, CompanyScheduleNotices.NotApproved,
                WithReason, "event not approved (organiser)", cancellationToken,
                alreadyTold: status => status != InvitationStatus.NotSent));
        }

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
        // Lane 2e-3 (D-14): the cancellation takes the guests' calendar entries away.
        entity.CalendarSequence++;
        var bookings = await CancelLinkedBookingsAsync(entity, $"{entity.EventNumber} was cancelled: {reason}", cancellationToken);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event cancelled: {EventNumber}, {Bookings} room booking(s) with it",
            entity.EventNumber, bookings.Count);

        // D-10: an approval still under way is withdrawn — there is nothing left to approve.
        await CancelApprovalAsync(entity, $"The event was cancelled: {reason}");

        var told = await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventCancelled,
            tokens =>
            {
                tokens["CancellationReason"] = entity.CancellationReason;
                return tokens;
            },
            "event cancelled", CompanyScheduleNotices.Cancelled,
            calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken);

        return new CompanyEventChangeDto
        {
            Event = await GetByIdAsync(entity.Id, cancellationToken),
            BookingsCancelled = bookings,
            Told = told,
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
        change.Told = await NotifyRescheduledAsync(entity, cancellationToken);
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
    public async Task<CompanyEventNoticeResultDto> SendRsvpRemindersAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        // F-33 (lane 2e-1): the sweep's rule — it refused only a cancelled event.
        Refuse(CompanyEventRules.RefuseChasing(ev, DateTime.UtcNow));

        return await ChaseRsvpsAsync(ev, DateTime.UtcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CompanyEventNoticeResultDto> SendEventRemindersAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        // F-33 (lane 2e-1): the sweep's rule — it refused only a cancelled event.
        Refuse(CompanyEventRules.RefuseReminding(ev, DateTime.UtcNow));

        return await RemindAsync(ev, DateTime.UtcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CompanyEventNoticeResultDto> SendUndeliveredInvitationsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        Refuse(CompanyEventRules.RefuseInviting(ev, DateTime.UtcNow));

        var result = await InviteWaitingGuestsAsync(ev, cancellationToken);
        if (result.Issued == 0)
            throw new InvalidOperationException($"Every invitation to {ev.EventName} has reached its guest, so there is nothing to send.");

        _logger.LogInformation("Undelivered invitations to {EventNumber} sent again: {Reached} of {Issued} reached",
            ev.EventNumber, result.Reached, result.Issued);
        return result;
    }

    /// <summary>
    /// Stamps a reminder or a chase as sent — only when it reached somebody (lane 2e-2, R4-6.3). One that reached
    /// nobody stays due, so the next pass of the sweep, or the button, tries again.
    /// </summary>
    /// <remarks>
    /// ⚠ The stamp used to be written on a run that delivered nothing — no mail server took an email — so the sweep
    /// never tried again. A run that reached only some is stamped (the user's ruling, 2026-10-05): those not reached
    /// are in its counts, not retried.
    /// </remarks>
    private async Task<CompanyEventNoticeResultDto> StampIfReachedAsync(
        CompanyEvent ev, CompanyEventNoticeResultDto result, Action<CompanyEvent> stamp, CancellationToken cancellationToken)
    {
        if (result.Reached == 0) return result;

        stamp(ev);
        await _eventRepository.UpdateAsync(ev);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.Stamped = true;
        return result;
    }

    /// <summary>
    /// Chases everybody who has not answered, and stamps the event so it is not chased again — by the
    /// button or the sweep, whichever comes second (round 4, lane N-b2) — once it reached somebody (lane 2e-2).
    /// </summary>
    private async Task<CompanyEventNoticeResultDto> ChaseRsvpsAsync(CompanyEvent ev, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var result = await NotifyParticipantsAsync(ev, CompanyScheduleEmailCatalog.Events.EventRsvpReminder,
            enrich: null, "RSVP reminder", CompanyScheduleNotices.RsvpChase,
            // ⚠ NotSent as well as Sent. A participant added before the invitation send existed
            // carries NotSent and has genuinely never been asked — chasing them is the first time
            // anybody has told them, which is exactly who this is for. Since lane 2e-2 that includes
            // a guest whose invitation reached nobody.
            filter: p => p.InvitationStatus is InvitationStatus.Sent or InvitationStatus.NotSent,
            change: false, cancellationToken: cancellationToken);

        return await StampIfReachedAsync(ev, result, e => e.RsvpReminderSentDate = nowUtc, cancellationToken);
    }

    /// <summary>
    /// Reminds every participant who has not declined, and stamps the event so it is not reminded
    /// again for this date — by the button or the sweep, whichever comes second (lane N-b2) — once it
    /// reached somebody (lane 2e-2).
    /// </summary>
    private async Task<CompanyEventNoticeResultDto> RemindAsync(CompanyEvent ev, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // ⚠ Declined participants are NOT reminded. They have said they are not coming; a reminder
        // is the system ignoring the answer it asked for.
        var daysUntil = (ev.StartDate.Date - nowUtc.Date).TotalDays;
        Dictionary<string, string?> WithDays(Dictionary<string, string?> tokens)
        {
            tokens["DaysUntil"] = daysUntil >= 1 ? ((int)daysUntil).ToString() : null;
            return tokens;
        }

        var result = await NotifyParticipantsAsync(ev, CompanyScheduleEmailCatalog.Events.EventReminder, WithDays,
            "event reminder", CompanyScheduleNotices.Reminder,
            filter: p => p.InvitationStatus != InvitationStatus.Declined,
            change: false, cancellationToken: cancellationToken);
        // D-11 (lane 2e-1): the organiser is reminded too, on the guest list or not — once.
        result.Add(await TellOrganiserAsync(ev, CompanyScheduleEmailCatalog.Events.EventReminder, CompanyScheduleNotices.Reminder,
            WithDays, "event reminder (organiser)", cancellationToken,
            actorToo: true, alreadyTold: status => status != InvitationStatus.Declined));

        return await StampIfReachedAsync(ev, result, e => e.ReminderSentDate = nowUtc, cancellationToken);
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
    ///
    /// <para><b>Only what reached somebody is stamped (lane 2e-2).</b> A reminder or chase that reached nobody — no
    /// mail server took an email and nobody it was for has a login — stays due, and every pass tries it again until
    /// the event begins or the reply-by date passes. ⚠ Under cross-module #40 this host's emails find no mail server
    /// even where one is set up, so only the in-app notices reach anybody until Platform fixes it.</para>
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
            // F-33 (lane 2e-1): the buttons' rule. The query above already narrows to it; this keeps them one rule.
            if (CompanyEventRules.RefuseReminding(ev, nowUtc) is not null) continue;

            if (ev.ReminderSentDate is null && ReminderDueOn(ev) is { } remindOn && remindOn <= today)
            {
                var reminded = Tally(run, await RemindAsync(ev, nowUtc, cancellationToken));
                // Lane 2e-2: an event with nobody to remind yet is neither — a guest added later is reminded.
                if (reminded.Issued > 0) (reminded.Stamped ? run.Reminded : run.RemindersLeftDue).Add(ev.EventNumber);
            }

            if (ev.RsvpReminderSentDate is null && CompanyEventRules.RefuseChasing(ev, nowUtc) is null
                && RsvpChaseDueOn(ev, lead) is { } chaseOn && chaseOn <= today)
            {
                var chased = Tally(run, await ChaseRsvpsAsync(ev, nowUtc, cancellationToken));
                if (chased.Issued > 0) (chased.Stamped ? run.RsvpChased : run.ChasesLeftDue).Add(ev.EventNumber);
            }
        }

        // Lane 2e-3 (F-34): each open task past its due date, its assignee chased once. The rule is IsOverdue's (due
        // before today, not completed or cancelled), on an event still going ahead or already held — post-event
        // tasks, the minutes say, come due after it. Not a cancelled event's, and not a leaver's: they would never be
        // reached, and would be tried every hour.
        var overdue = await TenantTasks(tenantId)
            .Include(t => t.Event).ThenInclude(e => e.Organizer)
            .Where(t => t.OverdueChasedAt == null && t.DueDate != null && t.DueDate < today
                        && t.Status != EventTaskStatus.Completed && t.Status != EventTaskStatus.Cancelled
                        && t.AssignedTo != null && t.AssignedTo.IsActive && !t.AssignedTo.IsDeleted
                        && !t.Event.IsDeleted && !t.Event.IsCancelled && t.Event.Status != EventStatus.Cancelled)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);
        foreach (var task in overdue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chased = await ChaseOverdueTaskAsync(task.Event, task, nowUtc, cancellationToken);
            (chased.Stamped ? run.TasksChased : run.TasksLeftDue).Add(task.Id);
        }

        if (run.PeopleIssued > 0 || overdue.Count > 0)
            _logger.LogInformation(
                "Company schedule reminders for tenant {TenantId}: reminded {Reminded}, chased {Chased}; reached {Reached} of {Issued} — "
                + "{Emails} email(s) taken, {NotTaken} not, {InApp} told in the app. Left due, reaching nobody: {RemindersLeftDue} {ChasesLeftDue}. "
                + "Overdue tasks chased {TasksChased}, left due {TasksLeftDue}",
                tenantId, run.Reminded.Count, run.RsvpChased.Count, run.PeopleReached, run.PeopleIssued,
                run.EmailsSent, run.EmailsNotTaken, run.ToldInApp, run.RemindersLeftDue, run.ChasesLeftDue,
                run.TasksChased.Count, run.TasksLeftDue.Count);
        return run;
    }

    private static CompanyEventNoticeResultDto Tally(CompanyScheduleReminderRunDto run, CompanyEventNoticeResultDto result)
    {
        run.PeopleIssued += result.Issued;
        run.PeopleReached += result.Reached;
        run.EmailsSent += result.Emailed;
        run.EmailsNotTaken += result.EmailsNotTaken;
        run.ToldInApp += result.ToldInApp;
        return result;
    }

    /// <summary>The day the sweep sends the reminder: <c>ReminderDaysBefore</c> before the start; null when off.</summary>
    private static DateTime? ReminderDueOn(CompanyEvent ev) =>
        ev.SendReminders && ev.ReminderDaysBefore is { } daysBefore
            ? ev.StartDate.Date.AddDays(-Math.Max(0, daysBefore))
            : null;

    /// <summary>The day the sweep chases unanswered invitations: the tenant's lead before the reply-by date.</summary>
    private static DateTime? RsvpChaseDueOn(CompanyEvent ev, int lead) =>
        ev.RequiresRsvp && ev.RsvpDeadline is { } deadline ? deadline.Date.AddDays(-Math.Max(0, lead)) : null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Guests and the register (company-schedule final closure, lane 2d)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>The F-45 unique indexes — one live guest row and one register row per employee per event.</summary>
    private const string GuestIndex = "UX_EventParticipant_Tenant_Event_Employee";
    private const string RegisterIndex = "UX_EventAttendance_Tenant_Event_Employee";

    /// <summary>This tenant's live guests, with their employee — the tenant inside the query (F-30).</summary>
    private IQueryable<EventParticipant> TenantGuests(Guid tenantId) =>
        _participantRepository.GetQueryable()
            .Include(p => p.Employee)
            .Where(p => p.TenantId == tenantId && !p.IsDeleted);

    /// <summary>This tenant's live register rows, with the person and who marked them.</summary>
    private IQueryable<EventAttendance> TenantRegister(Guid tenantId) =>
        _attendanceRepository.GetQueryable()
            .Include(a => a.Employee)
            .Include(a => a.MarkedBy)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

    private static string GuestName(EventParticipant p) =>
        p.Employee is not null ? $"{p.Employee.FirstName} {p.Employee.LastName}".Trim() : p.ExternalParticipantName ?? "The guest";

    private sealed record EmployeeRef(string Name, bool IsActive);

    /// <summary>This tenant's employee, live or not; null when there is none.</summary>
    private async Task<EmployeeRef?> FindEmployeeAsync(Guid employeeId, Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(x => x.Id == employeeId && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.FirstName, x.LastName, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new EmployeeRef($"{row.FirstName} {row.LastName}".Trim(), row.IsActive);
    }

    /// <summary>
    /// This tenant's employee and still employed — a guest or a task's assignee (F-10, F-35). Refuses as a
    /// rule (a 422), naming who, rather than failing at the database or storing another tenant's id.
    /// </summary>
    private async Task<EmployeeRef> RequireActiveEmployeeAsync(
        Guid employeeId, Guid tenantId, string who, CancellationToken cancellationToken)
    {
        var person = await FindEmployeeAsync(employeeId, tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"The {who} chosen was not found. Choose the {who} again.");
        if (!person.IsActive)
            throw new InvalidOperationException($"{person.Name} is no longer an active employee. Choose someone who is.");
        return person;
    }

    /// <summary>
    /// Saves, answering a refusal by one of the F-45 unique indexes as the sentence it means: two requests
    /// adding the same person at once both pass the check, and the database refuses the second.
    /// </summary>
    private async Task SaveRefusingDuplicateAsync(string index, string refusal, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 2601 or 2627 } sql
                                           && sql.Message.Contains(index, StringComparison.OrdinalIgnoreCase))
        {
            // The refused row stays tracked; a later save in this request must not try it again.
            _unitOfWork.ClearTrackedChanges();
            throw new InvalidOperationException(refusal);
        }
    }

    /// <summary>
    /// Checks a guest (lane 2d): an outside guest needs a name and an address; a new employee guest must be
    /// this tenant's and still employed (F-35); nobody is invited twice — by employee, or by address.
    /// </summary>
    private async Task CheckGuestAsync(CompanyEvent e, EventParticipant guest, bool isNew, CancellationToken cancellationToken)
    {
        var name = guest.ExternalParticipantName;
        var email = guest.ExternalParticipantEmail;
        var organisation = guest.ExternalParticipantOrganization;
        Refuse(CompanyEventRules.NormaliseGuest(guest.EmployeeId, ref name, ref email, ref organisation));
        guest.ExternalParticipantName = name;
        guest.ExternalParticipantEmail = email;
        guest.ExternalParticipantOrganization = organisation;
        guest.SpecialRequirements = CompanyEventRules.Clean(guest.SpecialRequirements);

        if (!Enum.IsDefined(guest.Role))
            throw new InvalidOperationException("Choose the guest's role: organiser, presenter, attendee or optional.");

        var others = TenantGuests(e.TenantId).Where(p => p.EventId == e.Id && p.Id != guest.Id);

        if (guest.EmployeeId is { } employeeId)
        {
            // An employee guest is fixed once invited: uninvite and invite the other person instead.
            if (!isNew) return;
            var person = await RequireActiveEmployeeAsync(employeeId, e.TenantId, "guest", cancellationToken);
            if (await others.AnyAsync(p => p.EmployeeId == employeeId, cancellationToken))
                throw new InvalidOperationException($"{person.Name} is already invited to {e.EventName}.");
            return;
        }

        var address = email!.ToLower();
        var taken = await others
            .Where(p => p.EmployeeId == null && p.ExternalParticipantEmail != null && p.ExternalParticipantEmail.ToLower() == address)
            .Select(p => p.ExternalParticipantName)
            .FirstOrDefaultAsync(cancellationToken);
        if (taken is not null)
            throw new InvalidOperationException($"{email} is already invited to {e.EventName}, as {taken}.");
    }

    /// <summary>
    /// Sends one guest the invitation (round 4, D6) — by email and in the app (lane 2e-1). Best-effort: the guest's
    /// place on the list is already saved, and an unreachable mail server must not undo it.
    /// </summary>
    /// <returns>
    /// Whether it reached them: an email the mail server took, or a notice in the app (lane 2e-2). Inviting oneself
    /// counts — they know — though nobody is told of their own act.
    /// </returns>
    private async Task<bool> SendInvitationAsync(
        CompanyEvent ev, EventParticipant guest, CompanyEventNoticeResultDto tally, CancellationToken cancellationToken)
    {
        // ⚠ F-35: a leaver is never invited.
        if (guest.Employee is { IsActive: false }) return false;
        tally.Issued++;
        // Lane 2e-1: nobody is told of their own act — inviting oneself.
        if (guest.EmployeeId is { } self && self == await _notices.ActorEmployeeIdAsync(cancellationToken))
        {
            tally.Reached++;
            return true;
        }

        var name = guest.Employee is not null
            ? $"{guest.Employee.FirstName} {guest.Employee.LastName}".Trim()
            : guest.ExternalParticipantName ?? "Colleague";
        var tokens = EventTokens(ev, name);
        tokens["IsRequired"] = guest.IsRequired ? "true" : null;
        tokens["SpecialRequirements"] = guest.SpecialRequirements;

        var address = guest.Employee?.EmailAddress ?? guest.ExternalParticipantEmail;
        // Lane 2e-3 (D-14): the invitation carries the calendar entry, at the event's current sequence — not for a
        // postponed event, which has no date to hold; the move to a new date sends it.
        var file = string.IsNullOrWhiteSpace(address) || ev.Status == EventStatus.Postponed
            ? null
            : CalendarFileFor(ev, HrCalendarMethod.Request, await CalendarOrganizerAsync(ev, cancellationToken),
                address, name, guest.IsRequired);
        var emailed = await SendEventEmailAsync(
            ev.TenantId, CompanyScheduleEmailCatalog.Events.EventInvitation, address, tokens, "event invitation", file);
        if (emailed) tally.Emailed++;
        else if (!string.IsNullOrWhiteSpace(address)) tally.EmailsNotTaken++;

        var inApp = guest.EmployeeId is { } employeeId
            && (await _notices.TellAsync(ev, CompanyScheduleNotices.Invited, CompanyScheduleNotices.ToGuest, [employeeId],
                cancellationToken: cancellationToken)).Contains(employeeId);
        if (inApp) tally.ToldInApp++;

        if (!emailed && !inApp)
        {
            _logger.LogInformation("The invitation to {EventNumber} reached nobody for {Guest}: left not delivered.",
                ev.EventNumber, GuestName(guest));
            return false;
        }
        tally.Reached++;
        return true;
    }

    /// <summary>
    /// Tells a guest who was invited that they are no longer (lane 2e-1) — by email and in the app; never of
    /// their own act, and not a leaver.
    /// </summary>
    private async Task TellRemovedGuestAsync(CompanyEvent ev, EventParticipant guest, CancellationToken cancellationToken)
    {
        if (guest.Employee is { IsActive: false }) return;
        if (guest.EmployeeId is { } self && self == await _notices.ActorEmployeeIdAsync(cancellationToken)) return;

        var name = guest.Employee is not null
            ? $"{guest.Employee.FirstName} {guest.Employee.LastName}".Trim()
            : guest.ExternalParticipantName ?? "Colleague";
        var address = guest.Employee?.EmailAddress ?? guest.ExternalParticipantEmail;
        // Lane 2e-3 (D-14): a cancellation of their entry alone — the event goes on for everyone else.
        var file = string.IsNullOrWhiteSpace(address)
            ? null
            : CalendarFileFor(ev, HrCalendarMethod.Cancel, await CalendarOrganizerAsync(ev, cancellationToken),
                address, name, guest.IsRequired);
        await SendEventEmailAsync(
            ev.TenantId, CompanyScheduleEmailCatalog.Events.EventGuestRemoved, address, EventTokens(ev, name),
            "guest removed", file);

        if (guest.EmployeeId is { } employeeId)
            await _notices.TellAsync(ev, CompanyScheduleNotices.Removed, CompanyScheduleNotices.ToGuest, [employeeId],
                cancellationToken: cancellationToken);
    }

    /// <remarks>
    /// Lane 2d: no invitation to a cancelled or completed event; an outside guest needs a name and an
    /// address; a leaver is refused (F-35); nobody twice, by employee or by address.
    /// </remarks>
    public async Task<EventParticipantDto> AddParticipantAsync(CreateEventParticipantDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ev = await GetOwnedEventAsync(createDto.EventId, cancellationToken);
        if (CompanyEventRules.IsClosed(ev))
            throw new InvalidOperationException($"{ev.EventName} is {ClosedState(ev)}, so nobody more can be invited.");

        var entity = createDto.ToEntity();
        if (entity.EmployeeId == Guid.Empty) entity.EmployeeId = null;
        entity.TenantId = tenantId;
        await CheckGuestAsync(ev, entity, isNew: true, cancellationToken);

        // F-33 (lane 2e-1): an event awaiting approval invites nobody yet — the invitation goes with the approval.
        // Lane 2e-2 (R4-6.3): Not sent until it reaches them; it was Sent on insert, before anything was sent.
        var waits = CompanyEventRules.IsAwaitingApproval(ev);
        entity.InvitationStatus = InvitationStatus.NotSent;
        entity.InvitationSentDate = null;

        await _participantRepository.AddAsync(entity);
        await SaveRefusingDuplicateAsync(GuestIndex, $"That employee is already invited to {ev.EventName}.", cancellationToken);

        var saved = await TenantGuests(tenantId).FirstAsync(p => p.Id == entity.Id, cancellationToken);
        _logger.LogInformation("Guest {Guest} invited to {EventNumber}", GuestName(saved), ev.EventNumber);

        if (!waits) await InviteAsync(ev, saved, new CompanyEventNoticeResultDto(), cancellationToken);
        return saved.ToDto();
    }

    public async Task<IEnumerable<EventParticipantDto>> GetParticipantsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var guests = await TenantGuests(tenantId)
            .Where(p => p.EventId == eventId)
            .OrderBy(p => p.Role)
            .ThenBy(p => p.Employee != null ? p.Employee.FirstName : p.ExternalParticipantName)
            .ToListAsync(cancellationToken);
        return guests.ToDtoList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// C-22 (D-9): the role, whether they are required, their special requirements, and an outside guest's
    /// name, address and organisation. An employee guest stays who they are. A corrected address is sent the
    /// invitation, since the first one went nowhere.
    /// </remarks>
    public async Task<EventParticipantDto> UpdateParticipantAsync(UpdateEventParticipantDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var guest = await TenantGuests(tenantId).FirstOrDefaultAsync(p => p.Id == updateDto.Id, cancellationToken)
            ?? throw new ArgumentException("That guest was not found.");
        var ev = await GetOwnedEventAsync(guest.EventId, cancellationToken);
        if (CompanyEventRules.IsClosed(ev))
            throw new InvalidOperationException($"{ev.EventName} is {ClosedState(ev)}; its guest list is part of its record.");

        var previousAddress = guest.ExternalParticipantEmail;
        guest.Role = updateDto.Role;
        guest.IsRequired = updateDto.IsRequired;
        guest.SpecialRequirements = updateDto.SpecialRequirements;
        guest.ExternalParticipantName = updateDto.ExternalParticipantName;
        guest.ExternalParticipantEmail = updateDto.ExternalParticipantEmail;
        guest.ExternalParticipantOrganization = updateDto.ExternalParticipantOrganization;
        await CheckGuestAsync(ev, guest, isNew: false, cancellationToken);

        var readdressed = guest.EmployeeId is null
            && !string.Equals(previousAddress, guest.ExternalParticipantEmail, StringComparison.OrdinalIgnoreCase);

        await _participantRepository.UpdateAsync(guest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Guest {Guest} of {EventNumber} updated", GuestName(guest), ev.EventNumber);

        // Lane 2e-2: marked sent (and dated) only once the new address took it — not while the event awaits approval,
        // whose approval sends it.
        if (readdressed && !CompanyEventRules.IsAwaitingApproval(ev))
            await InviteAsync(ev, guest, new CompanyEventNoticeResultDto(), cancellationToken);
        return (await TenantGuests(tenantId).FirstAsync(p => p.Id == guest.Id, cancellationToken)).ToDto();
    }

    /// <remarks>
    /// F-11: only accepted, declined or tentative is an answer, and only from a guest of the event in the
    /// route. A cancelled or completed event's invitations are closed.
    /// </remarks>
    public async Task<bool> RespondToInvitationAsync(Guid eventId, RespondToEventInvitationDto responseDto, CancellationToken cancellationToken = default)
    {
        if (!CompanyEventRules.IsAnswer(responseDto.Response))
            throw new InvalidOperationException("Record the answer as accepted, declined or tentative.");

        var tenantId = GetTenantId();
        var guest = await TenantGuests(tenantId)
                .FirstOrDefaultAsync(p => p.Id == responseDto.ParticipantId && p.EventId == eventId, cancellationToken)
            ?? throw new ArgumentException("That guest is not on this event's list.");
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        if (CompanyEventRules.IsClosed(ev))
            throw new InvalidOperationException(
                $"{ev.EventName} is {ClosedState(ev)}, so its invitations can no longer be answered.");

        guest.InvitationStatus = responseDto.Response;
        guest.ResponseDate = DateTime.UtcNow;
        guest.ResponseComments = CompanyEventRules.Clean(responseDto.ResponseComments);

        await _participantRepository.UpdateAsync(guest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{Guest} answered {Answer} for {EventNumber}", GuestName(guest), responseDto.Response, ev.EventNumber);
        return true;
    }

    /// <remarks>
    /// Organiser work, on Write (lane 2d) — it needed Admin. Not from a cancelled or completed event, whose
    /// guest list is its record.
    /// </remarks>
    public async Task<bool> RemoveParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var guest = await TenantGuests(tenantId).FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken)
            ?? throw new ArgumentException("That guest was not found.");
        var ev = await GetOwnedEventAsync(guest.EventId, cancellationToken);
        if (CompanyEventRules.IsClosed(ev))
            throw new InvalidOperationException($"{ev.EventName} is {ClosedState(ev)}; its guest list is part of its record.");

        // Lane 2e-3 (D-14): a guest who held an entry is sent its cancellation, which must outrank the entry they hold.
        if (guest.InvitationStatus != InvitationStatus.NotSent) ev.CalendarSequence++;

        await _participantRepository.DeleteAsync(guest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Guest {Guest} removed from {EventNumber}", GuestName(guest), ev.EventNumber);

        // Lane 2e-1: a guest who was invited hears they no longer are (one still waiting for approval never heard).
        if (guest.InvitationStatus != InvitationStatus.NotSent) await TellRemovedGuestAsync(ev, guest, cancellationToken);
        return true;
    }

    #endregion

    #region Attendance Operations

    /// <remarks>
    /// <para>Lane 2d: once the event has started and never on a cancelled one; the check-in on one of its
    /// days and not still to come. The person must be this tenant's — but may since have left: they still
    /// attended.</para>
    ///
    /// <para>⚠ <b>F-1.</b> Marking again — how the register is corrected — stamped the check-in with the
    /// moment of the correction, for an absence too. It now keeps the check-in unless a new one is given,
    /// and an absence carries none.</para>
    /// </remarks>
    public async Task<EventAttendanceDto> MarkAttendanceAsync(MarkEventAttendanceDto markDto, Guid markedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ev = await GetOwnedEventAsync(markDto.EventId, cancellationToken);
        var now = DateTime.UtcNow;
        Refuse(CompanyEventRules.RefuseMarking(ev, now));

        var person = await FindEmployeeAsync(markDto.EmployeeId, tenantId, cancellationToken)
            ?? throw new InvalidOperationException("The employee chosen was not found. Choose them again.");

        var row = await TenantRegister(tenantId)
            .FirstOrDefaultAsync(a => a.EventId == ev.Id && a.EmployeeId == markDto.EmployeeId, cancellationToken);
        var isNew = row is null;
        row ??= new EventAttendance { TenantId = tenantId, EventId = ev.Id, EmployeeId = markDto.EmployeeId };

        if (markDto.Attended)
        {
            var checkIn = markDto.CheckInTime ?? row.CheckInTime ?? now;
            Refuse(CompanyEventRules.RefuseCheckIn(ev, checkIn, now));
            if (row.CheckOutTime is { } checkedOut && checkedOut < checkIn)
                throw new InvalidOperationException(
                    $"{person.Name} was checked out {CompanyEventRules.Describe(checkedOut)}, before that check-in. "
                  + "Give the time they arrived, or remove the row and mark it again.");
            row.CheckInTime = checkIn;
            row.AbsenceReason = null;
        }
        else
        {
            row.CheckInTime = null;
            row.CheckOutTime = null;
            row.AbsenceReason = CompanyEventRules.Clean(markDto.AbsenceReason);
        }

        row.Attended = markDto.Attended;
        row.Notes = CompanyEventRules.Clean(markDto.Notes);
        row.MarkedById = markedById;

        if (isNew) await _attendanceRepository.AddAsync(row);
        else await _attendanceRepository.UpdateAsync(row);
        await SaveRefusingDuplicateAsync(RegisterIndex,
            $"{person.Name} is already on {ev.EventName}'s register. Correct that row instead.", cancellationToken);

        _logger.LogInformation("Attendance marked for {EventNumber}: {Employee} {Attended}",
            ev.EventNumber, person.Name, markDto.Attended ? "present" : "absent");

        return (await TenantRegister(tenantId).FirstAsync(a => a.Id == row.Id, cancellationToken)).ToDto();
    }

    public async Task<IEnumerable<EventAttendanceDto>> GetAttendanceAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var rows = await TenantRegister(tenantId)
            .Where(a => a.EventId == eventId)
            .OrderBy(a => a.Employee.FirstName)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync(cancellationToken);
        return rows.ToDtoList();
    }

    /// <remarks>Lane 2d: only someone checked in, once, and not on a cancelled event.</remarks>
    public async Task<bool> CheckOutAsync(CheckOutEventDto checkOutDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await TenantRegister(tenantId).FirstOrDefaultAsync(a => a.Id == checkOutDto.AttendanceId, cancellationToken)
            ?? throw new ArgumentException("That attendance record was not found.");
        var ev = await GetOwnedEventAsync(row.EventId, cancellationToken);
        if (ev.IsCancelled || ev.Status == EventStatus.Cancelled)
            throw new InvalidOperationException($"{ev.EventName} was cancelled, so there is nobody to check out.");

        var name = row.Employee is not null ? $"{row.Employee.FirstName} {row.Employee.LastName}".Trim() : "They";
        var now = DateTime.UtcNow;
        Refuse(CompanyEventRules.RefuseCheckOut(name, row, now));

        row.CheckOutTime = now;
        row.Notes = CompanyEventRules.Clean(checkOutDto.Notes) ?? row.Notes;

        await _attendanceRepository.UpdateAsync(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-out recorded for {EventNumber}: {Employee}", ev.EventNumber, name);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>C-21 (D-9): a correction to the register, on Write — the row must be this event's.</remarks>
    public async Task RemoveAttendanceAsync(Guid eventId, Guid attendanceId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var row = await TenantRegister(tenantId)
                .FirstOrDefaultAsync(a => a.Id == attendanceId && a.EventId == eventId, cancellationToken)
            ?? throw new ArgumentException("That attendance record is not on this event's register.");

        await _attendanceRepository.DeleteAsync(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attendance row {AttendanceId} removed from event {EventId}", attendanceId, eventId);
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

    /// <summary>This tenant's live tasks, with their assignee — the tenant inside the query (F-30).</summary>
    private IQueryable<EventTask> TenantTasks(Guid tenantId) =>
        _taskRepository.GetQueryable()
            .Include(t => t.AssignedTo)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

    /// <summary>
    /// Chases a task's assignee about it being overdue (lane 2e-3, F-34) — by email and in the app, and stamped only
    /// when that reached them (lane 2e-2's rule), so the hourly sweep sends it once and never again; one that reached
    /// nobody stays due for the next pass.
    /// </summary>
    /// <remarks>
    /// The assignee only (the user's ruling): the organiser sees Overdue on the event page. Told even if they are the
    /// one who ran the sweep — like a reminder, it is about the date, not an act.
    /// </remarks>
    private async Task<CompanyEventNoticeResultDto> ChaseOverdueTaskAsync(
        CompanyEvent ev, EventTask task, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var assignee = task.AssignedTo!;
        var result = new CompanyEventNoticeResultDto
        {
            Issued = 1,
            MailServerSetUp = await MailServerSetUpAsync(ev.TenantId, cancellationToken),
        };

        var tokens = EventTokens(ev, $"{assignee.FirstName} {assignee.LastName}".Trim());
        tokens["TaskDescription"] = task.TaskDescription;
        tokens["TaskDue"] = task.DueDate?.ToString("dddd, d MMMM yyyy");
        tokens["TaskPriority"] = task.Priority.ToString();
        var emailed = await SendEventEmailAsync(ev.TenantId, CompanyScheduleEmailCatalog.Events.EventTaskOverdue,
            assignee.EmailAddress, tokens, "event task overdue");
        if (emailed) result.Emailed = 1;
        else if (!string.IsNullOrWhiteSpace(assignee.EmailAddress)) result.EmailsNotTaken = 1;

        var inApp = (await _notices.TellAsync(ev, CompanyScheduleNotices.TaskOverdue, CompanyScheduleNotices.ToAssignee,
            [assignee.Id],
            new Dictionary<string, object>
            {
                ["Task"] = task.TaskDescription.Length <= 120 ? task.TaskDescription : task.TaskDescription[..117] + "…",
                ["DueOn"] = task.DueDate is { } due ? due.ToString("d MMM yyyy") : string.Empty,
            },
            actorToo: true, cancellationToken: cancellationToken)).Contains(assignee.Id);
        result.ToldInApp = inApp ? 1 : 0;
        result.Reached = emailed || inApp ? 1 : 0;

        if (result.Reached > 0)
        {
            task.OverdueChasedAt = nowUtc;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            result.Stamped = true;
        }
        return result;
    }

    /// <summary>
    /// Tells an employee a task is theirs (lane 2e-1, F-34) — by email and in the app; never of their own act,
    /// and not a leaver kept on a task already theirs.
    /// </summary>
    private async Task TellAssigneeAsync(CompanyEvent ev, EventTask task, CancellationToken cancellationToken)
    {
        if (task.AssignedTo is not { IsActive: true } assignee) return;
        if (assignee.Id == await _notices.ActorEmployeeIdAsync(cancellationToken)) return;

        var tokens = EventTokens(ev, $"{assignee.FirstName} {assignee.LastName}".Trim());
        tokens["TaskDescription"] = task.TaskDescription;
        tokens["TaskDue"] = task.DueDate?.ToString("dddd, d MMMM yyyy");
        tokens["TaskPriority"] = task.Priority.ToString();
        await SendEventEmailAsync(ev.TenantId, CompanyScheduleEmailCatalog.Events.EventTaskAssigned,
            assignee.EmailAddress, tokens, "event task assigned");

        await _notices.TellAsync(ev, CompanyScheduleNotices.TaskAssigned, CompanyScheduleNotices.ToAssignee, [assignee.Id],
            new Dictionary<string, object>
            {
                ["Task"] = task.TaskDescription.Length <= 120 ? task.TaskDescription : task.TaskDescription[..117] + "…",
                ["Due"] = task.DueDate is { } due ? $" — due {due:d MMM yyyy}" : string.Empty,
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>"Book the caterer", cut to a phrase a refusal can quote.</summary>
    private static string Quote(EventTask t) =>
        t.TaskDescription.Length <= 60 ? $"\"{t.TaskDescription}\"" : $"\"{t.TaskDescription[..57]}…\"";

    /// <summary>
    /// Checks a task (lane 2d): a description, a stage and a priority; a new assignee this tenant's and still
    /// employed (F-10, F-35). An assignee who has since left can stay on a task already theirs.
    /// </summary>
    private async Task CheckTaskAsync(EventTask t, Guid? previousAssignee, CancellationToken cancellationToken)
    {
        t.TaskDescription = t.TaskDescription?.Trim() ?? string.Empty;
        if (t.TaskDescription.Length == 0)
            throw new InvalidOperationException("Describe the task.");
        if (!Enum.IsDefined(t.Category))
            throw new InvalidOperationException("Choose the stage: before, during or after the event.");
        if (!Enum.IsDefined(t.Priority))
            throw new InvalidOperationException("Choose the priority: critical, high, medium or low.");

        if (t.AssignedToId == Guid.Empty) t.AssignedToId = null;
        if (t.AssignedToId is { } assignee && assignee != previousAssignee)
            await RequireActiveEmployeeAsync(assignee, t.TenantId, "assignee", cancellationToken);
    }

    public async Task<EventTaskDto> AddTaskAsync(CreateEventTaskDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ev = await GetOwnedEventAsync(createDto.EventId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.Status = EventTaskStatus.NotStarted;
        await CheckTaskAsync(entity, previousAssignee: null, cancellationToken);

        await _taskRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task added to {EventNumber}: {TaskId}", ev.EventNumber, entity.Id);
        var saved = await TenantTasks(tenantId).FirstAsync(t => t.Id == entity.Id, cancellationToken);
        if (saved.AssignedToId is not null) await TellAssigneeAsync(ev, saved, cancellationToken);
        return saved.ToDto();
    }

    public async Task<IEnumerable<EventTaskDto>> GetTasksAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEventAsync(eventId, cancellationToken);
        var tasks = await TenantTasks(tenantId)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Priority)
            .ToListAsync(cancellationToken);
        return tasks.ToDtoList();
    }

    /// <remarks>
    /// <para>⚠ <b>F-12.</b> Completed through the edit had no completion date. It now gets one; a task taken
    /// back out of Completed loses it and its notes. Overdue is refused: it is worked out from the due date
    /// on every read.</para>
    ///
    /// <para>⚠ <b>F-46.</b> The answer is re-read after the save: it returned the entity with the OLD
    /// assignee's name after a reassignment.</para>
    /// </remarks>
    public async Task<EventTaskDto> UpdateTaskAsync(UpdateEventTaskDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await TenantTasks(tenantId).FirstOrDefaultAsync(t => t.Id == updateDto.Id, cancellationToken)
            ?? throw new ArgumentException("Task not found");

        if (!CompanyEventRules.IsSettableTaskStatus(updateDto.Status))
            throw new InvalidOperationException(updateDto.Status == EventTaskStatus.Overdue
                ? "Overdue is worked out from the due date. Set where the task stands: not started, in progress, completed or cancelled."
                : "Choose where the task stands: not started, in progress, completed or cancelled.");

        var previousAssignee = entity.AssignedToId;
        var previousDue = entity.DueDate;
        updateDto.UpdateEntity(entity);
        await CheckTaskAsync(entity, previousAssignee, cancellationToken);

        // Lane 2e-3 (F-34): a new due date, or a new assignee, is a new overdue — the sweep may chase it once more.
        if (entity.DueDate?.Date != previousDue?.Date || entity.AssignedToId != previousAssignee)
            entity.OverdueChasedAt = null;

        if (entity.Status == EventTaskStatus.Completed)
        {
            entity.CompletionDate ??= DateTime.UtcNow;
        }
        else
        {
            entity.CompletionDate = null;
            entity.CompletionNotes = null;
        }

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task updated: {TaskId}", updateDto.Id);
        var saved = await TenantTasks(tenantId).FirstAsync(t => t.Id == entity.Id, cancellationToken);
        // Lane 2e-1 (F-34): a task passed to somebody new tells them.
        if (saved.AssignedToId is { } assignee && assignee != previousAssignee)
            await TellAssigneeAsync(await GetOwnedEventAsync(saved.EventId, cancellationToken), saved, cancellationToken);
        return saved.ToDto();
    }

    /// <remarks>Lane 2d: not twice, and not a cancelled task — reopen it first.</remarks>
    public async Task<bool> CompleteTaskAsync(CompleteEventTaskDto completeDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await TenantTasks(tenantId).FirstOrDefaultAsync(t => t.Id == completeDto.TaskId, cancellationToken)
            ?? throw new ArgumentException("Task not found");

        switch (entity.Status)
        {
            case EventTaskStatus.Completed:
                throw new InvalidOperationException(entity.CompletionDate is { } done
                    ? $"{Quote(entity)} was already completed, {CompanyEventRules.Describe(done)}."
                    : $"{Quote(entity)} is already completed.");
            case EventTaskStatus.Cancelled:
                throw new InvalidOperationException(
                    $"{Quote(entity)} was cancelled. Reopen it — set it in progress — before completing it.");
        }

        entity.Status = EventTaskStatus.Completed;
        entity.CompletionDate = DateTime.UtcNow;
        entity.CompletionNotes = CompanyEventRules.Clean(completeDto.CompletionNotes);

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Event task completed: {TaskId}", completeDto.TaskId);
        return true;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await TenantTasks(tenantId).FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new ArgumentException("Task not found");

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
