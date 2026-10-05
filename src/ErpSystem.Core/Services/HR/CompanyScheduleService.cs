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

    /// <summary>The company's days off — public holidays and company-wide closures — an occurrence is flagged on (lane 2f-1).</summary>
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly IHrClosureCalendar _closureCalendar;

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
        CompanyScheduleNotices notices,
        IHrWorkingDayCalculator workingDays,
        IHrClosureCalendar closureCalendar)
    {
        _workingDays = workingDays;
        _closureCalendar = closureCalendar;
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
    private Task<bool> SendEventEmailAsync(
        Guid tenantId, string eventKey, string? toEmail, Dictionary<string, string?> tokens, string description,
        EmailAttachmentDto? calendarFile = null) =>
        SendEventEmailWithFilesAsync(tenantId, eventKey, toEmail, tokens, description,
            calendarFile is null ? null : new[] { calendarFile });

    /// <summary>
    /// <see cref="SendEventEmailAsync"/> with any number of calendar files — a series email carries one per date
    /// (lane 2f-2a, D-12: the user's ruling).
    /// </summary>
    private async Task<bool> SendEventEmailWithFilesAsync(
        Guid tenantId, string eventKey, string? toEmail, Dictionary<string, string?> tokens, string description,
        IReadOnlyList<EmailAttachmentDto>? calendarFiles)
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
                calendarFiles is { Count: > 0 } ? calendarFiles : null);

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
        var dto = entity.ToDto();
        await FillSeriesCountsAsync([dto], entity.TenantId, cancellationToken);
        return dto;
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

        // Lane 2f-1: the series it belongs to, each occurrence flagged where it falls on a day the company does not
        // work (D-12) — and this one's own flag, series or not.
        var spans = new List<(Guid Id, DateTime Start, DateTime End)> { (entity.Id, entity.StartDate, entity.EndDate) };
        if (entity.RecurrenceSeriesId is { } seriesId)
        {
            detail.SeriesOccurrences = await SeriesOccurrencesAsync(seriesId, tenantId, cancellationToken);
            detail.OccurrenceCount = detail.SeriesOccurrences.Count;
            spans = detail.SeriesOccurrences.Select(o => (o.Id, o.StartDate, o.EndDate)).ToList();
        }
        var notes = await DayOffNotesAsync(tenantId, spans, cancellationToken);
        foreach (var o in detail.SeriesOccurrences) o.DayOffNote = notes.GetValueOrDefault(o.Id);
        detail.DayOffNote = notes.GetValueOrDefault(entity.Id);
        // Lane 2h (C-51): the drill that made it, worded and linked.
        detail.Source = await SourceOfAsync(entity, cancellationToken);
        return detail;
    }

    public async Task<IEnumerable<CompanyEventDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // The register shows the site; TenantEvents includes it — without it the register read blank
        // while the detail page showed it, which looked like missing data rather than a missing Include.
        var tenantId = GetTenantId();
        var entities = await TenantEvents(tenantId).ToListAsync(cancellationToken);
        var dtos = entities.ToDtoList().ToList();
        // Lane 2f-1: "3 of 10" beside an occurrence in the register.
        await FillSeriesCountsAsync(dtos, tenantId, cancellationToken);
        return dtos;
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
        var dtos = items.ToDtoList().ToList();
        await FillSeriesCountsAsync(dtos, GetTenantId(), cancellationToken);

        return new PagedResult<CompanyEventDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2g-1 — the register: search, paging, export (D-9; C-10…C-13)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>The most rows an export holds — the register's whole history on UAT is a few hundred.</summary>
    private const int MaxExportRows = 10_000;

    /// <summary>"InProgress" → "In Progress", for a CSV a person reads.</summary>
    private static string Words(Enum value) =>
        System.Text.RegularExpressions.Regex.Replace(value.ToString(), "([a-z])([A-Z])", "$1 $2");

    /// <summary>
    /// The events a search finds — this tenant's (F-30), untracked, with no includes: callers select what they need from
    /// it. Text is matched in the name, the number, the venue and the organiser's name; dates by overlap (lane 2a).
    /// </summary>
    private IQueryable<CompanyEvent> EventSearchQuery(Guid tenantId, CompanyEventSearchDto search)
    {
        var query = _eventRepository.GetQueryable().AsNoTracking().Where(e => e.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var term = search.Text.Trim();
            query = query.Where(e => e.EventName.Contains(term) || e.EventNumber.Contains(term)
                                     || (e.VenueName != null && e.VenueName.Contains(term))
                                     || (e.Organizer.FirstName + " " + e.Organizer.LastName).Contains(term));
        }
        if (search.Status is { } status) query = query.Where(e => e.Status == status);
        if (search.Category is { } category) query = query.Where(e => e.Category == category);
        if (search.LocationId is { } site) query = query.Where(e => e.LocationId == site);
        if (search.OrganizationUnitId is { } unit) query = query.Where(e => e.OrganizationUnitId == unit);
        if (search.OrganizerId is { } organiser) query = query.Where(e => e.OrganizerId == organiser);
        if (search.SeriesId is { } seriesId) query = query.Where(e => e.RecurrenceSeriesId == seriesId);
        if (search.From is { } from)
        {
            var first = from.Date;
            query = query.Where(e => e.EndDate >= first);
        }
        if (search.To is { } to)
        {
            var last = to.Date;
            query = query.Where(e => e.StartDate <= last);
        }
        return query;
    }

    /// <summary>The search's order: newest first by default, a series in its own order.</summary>
    private static IOrderedQueryable<CompanyEvent> SortEvents(IQueryable<CompanyEvent> query, CompanyEventSearchDto search) =>
        (search.Sort ?? (search.SeriesId is null ? "-start" : "occurrence")).Trim().ToLowerInvariant() switch
        {
            "start" => query.OrderBy(e => e.StartDate).ThenBy(e => e.StartTime).ThenBy(e => e.EventNumber),
            "name" => query.OrderBy(e => e.EventName).ThenBy(e => e.StartDate),
            "number" => query.OrderBy(e => e.EventNumber),
            "-number" => query.OrderByDescending(e => e.EventNumber),
            "occurrence" => query.OrderBy(e => e.OccurrenceNumber).ThenBy(e => e.StartDate),
            _ => query.OrderByDescending(e => e.StartDate).ThenByDescending(e => e.StartTime).ThenByDescending(e => e.EventNumber),
        };

    /// <inheritdoc />
    /// <remarks>
    /// ⚠ The page's ids first, sorted on narrow rows, then those events with their names. Sorting the joined rows —
    /// each carrying a whole <c>Employee</c> — is what asked UAT's server for 387 MB at 2e-3.
    /// </remarks>
    public async Task<PagedResult<CompanyEventDto>> SearchAsync(CompanyEventSearchDto search, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var page = Math.Max(1, search.Page);
        var size = Math.Clamp(search.PageSize, 1, 200);
        var query = EventSearchQuery(tenantId, search);

        var total = await query.CountAsync(cancellationToken);
        var ids = await SortEvents(query, search).Select(e => e.Id)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(cancellationToken);
        var rows = ids.Count == 0
            ? new List<CompanyEvent>()
            : await TenantEvents(tenantId).AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);
        var position = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var dtos = rows.OrderBy(e => position[e.Id]).ToDtoList().ToList();
        await FillSeriesCountsAsync(dtos, tenantId, cancellationToken);

        return new PagedResult<CompanyEventDto> { Items = dtos, TotalCount = total, Page = page, PageSize = size };
    }

    /// <inheritdoc />
    /// <remarks>Narrow rows, in the search's order, up to <see cref="MaxExportRows"/>.</remarks>
    public async Task<byte[]> ExportCsvAsync(CompanyEventSearchDto search, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rows = await SortEvents(EventSearchQuery(tenantId, search), search)
            .Select(e => new
            {
                e.EventNumber,
                e.EventName,
                e.Category,
                e.Type,
                e.StartDate,
                e.StartTime,
                e.EndDate,
                e.EndTime,
                e.IsAllDayEvent,
                Site = e.SiteLocation != null ? e.SiteLocation.Name : null,
                e.VenueName,
                Unit = e.OrganizationUnit != null ? e.OrganizationUnit.Name : null,
                e.Scope,
                Organiser = e.Organizer.FirstName + " " + e.Organizer.LastName,
                e.Status,
                e.IsCancelled,
                e.RequiresApproval,
                e.ApprovalDate,
                e.RecurrenceSeriesId,
                e.OccurrenceNumber,
            })
            .Take(MaxExportRows)
            .ToListAsync(cancellationToken);

        var seriesIds = rows.Where(r => r.RecurrenceSeriesId != null).Select(r => r.RecurrenceSeriesId!.Value).Distinct().ToList();
        var seriesSize = seriesIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _eventRepository.GetQueryable().AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.RecurrenceSeriesId != null && seriesIds.Contains(e.RecurrenceSeriesId.Value))
                .GroupBy(e => e.RecurrenceSeriesId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        static string? Clock(TimeSpan? t) => t is { } v ? v.ToString(@"hh\:mm") : null;

        return CompanyScheduleCsv.Build(
            new[]
            {
                "Number", "Event", "Category", "Type", "Start date", "Start time", "End date", "End time", "All day",
                "Site", "Venue", "Unit", "Audience", "Organiser", "Status", "Approval", "Occurrence",
            },
            rows.Select(r => new[]
            {
                r.EventNumber, r.EventName, Words(r.Category), Words(r.Type),
                r.StartDate.ToString("yyyy-MM-dd"), r.IsAllDayEvent ? null : Clock(r.StartTime),
                r.EndDate.ToString("yyyy-MM-dd"), r.IsAllDayEvent ? null : Clock(r.EndTime), r.IsAllDayEvent ? "Yes" : "No",
                r.Site, r.VenueName, r.Unit, Words(r.Scope), r.Organiser.Trim(), Words(r.Status),
                !r.RequiresApproval ? "Not needed"
                    : r.ApprovalDate is { } approved ? $"Approved {approved:yyyy-MM-dd}"
                    : r.IsCancelled ? null : "Awaiting",
                r.RecurrenceSeriesId is { } series && r.OccurrenceNumber is { } number
                    ? $"{number} of {seriesSize.GetValueOrDefault(series)}"
                    : null,
            }));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2g-2 — event against event (C-15: the user's rulings, as D-9)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The live events of this tenant that clash with <paramref name="e"/> by <see cref="CompanyEventRules.ClashOf"/> —
    /// refusals first — leaving out itself and its own series (a series' dates never clash with each other).
    /// </summary>
    /// <remarks>
    /// The database narrows to the live events whose days touch <paramref name="e"/>'s and whose site could be the same;
    /// the rule decides the rest. <paramref name="e"/> may be unsaved, or changed in memory and not yet saved.
    /// </remarks>
    private async Task<List<(CompanyEvent Other, EventClash Kind)>> ClashesOfAsync(CompanyEvent e, CancellationToken cancellationToken)
    {
        if (!CompanyEventRules.IsLive(e)) return [];
        var first = e.StartDate.Date;
        var last = e.EndDate.Date;
        var query = _eventRepository.GetQueryable().AsNoTracking()
            .Include(x => x.SiteLocation)
            .Include(x => x.OrganizationUnit)
            .Where(x => x.TenantId == e.TenantId && x.Id != e.Id && !x.IsCancelled
                        && x.Status != EventStatus.Cancelled && x.Status != EventStatus.Completed && x.Status != EventStatus.Postponed
                        && x.StartDate <= last && x.EndDate >= first);
        if (e.RecurrenceSeriesId is { } seriesId) query = query.Where(x => x.RecurrenceSeriesId != seriesId);
        if (e.LocationId is { } site) query = query.Where(x => x.LocationId == null || x.LocationId == site);

        return (await query.ToListAsync(cancellationToken))
            .Select(x => (Other: x, Kind: CompanyEventRules.ClashOf(e, x)))
            .Where(c => c.Kind != EventClash.None)
            .OrderByDescending(c => c.Kind).ThenBy(c => c.Other.StartDate).ThenBy(c => c.Other.StartTime)
            .ToList();
    }

    /// <summary>The sentence a clash answers with — a refusal says what to do about it; a warning what to check.</summary>
    private static string ClashMessage(CompanyEvent e, CompanyEvent other, EventClash kind)
    {
        var otherNamed = $"{other.EventNumber} {other.EventName} ({CompanyEventRules.Describe(EventWindow.Of(other))})";
        var place = e.LocationId is { } site && other.LocationId == site
            ? $"at {other.SiteLocation?.Name ?? "the same site"}"
            : "and one of them is for every site";
        if (kind == EventClash.Refused)
        {
            var forWhom = CompanyEventRules.AudienceRuleOf(other)?.TargetType == HrAudienceTargetType.AllEmployees
                ? "the whole company"
                : other.OrganizationUnit?.Name ?? "the same unit";
            return $"{e.EventName} ({CompanyEventRules.Describe(EventWindow.Of(e))}) would clash with {otherNamed}: both are for "
                   + $"{forWhom}, at the same time, {place}. Move one of them, or change who it is for.";
        }
        return $"{otherNamed} is at the same time, {place}, for: "
               + $"{CompanyEventRules.DescribeAudience(other, other.OrganizationUnit?.Name)}. Check the same people are not needed at both.";
    }

    /// <summary>Refuses an event that would clash with another the server does not allow beside it (C-15), naming it.</summary>
    private async Task RefuseClashAsync(CompanyEvent e, CancellationToken cancellationToken)
    {
        var refused = (await ClashesOfAsync(e, cancellationToken)).FirstOrDefault(c => c.Kind == EventClash.Refused);
        if (refused.Other is { } other)
            throw new InvalidOperationException(ClashMessage(e, other, EventClash.Refused));
    }

    /// <summary>The overlaps the server allows, as warnings for the save's answer (C-15) — named by date for a series.</summary>
    private async Task<List<string>> ClashWarningsAsync(CompanyEvent e, bool nameTheDate, CancellationToken cancellationToken) =>
        (await ClashesOfAsync(e, cancellationToken))
            .Where(c => c.Kind == EventClash.Warning)
            .Select(c => (nameTheDate ? $"{e.EventNumber}, {SeriesDateLine(e)}: " : string.Empty) + ClashMessage(e, c.Other, EventClash.Warning))
            .ToList();

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventClashDto>> FindClashesAsync(EventClashQueryDto query, CancellationToken cancellationToken = default)
    {
        var allDay = query.IsAllDayEvent || query.StartTime is null || query.EndTime is null;
        var probe = new CompanyEvent
        {
            Id = query.ExcludeId ?? Guid.Empty,
            TenantId = GetTenantId(),
            EventName = "This event",
            StartDate = query.StartDate.Date,
            StartTime = allDay ? null : query.StartTime,
            EndDate = query.EndDate.Date < query.StartDate.Date ? query.StartDate.Date : query.EndDate.Date,
            EndTime = allDay ? null : query.EndTime,
            IsAllDayEvent = allDay,
            Scope = query.Scope,
            Visibility = query.Visibility,
            OrganizationUnitId = query.OrganizationUnitId,
            LocationId = query.LocationId,
            RecurrenceSeriesId = query.SeriesId,
            Status = EventStatus.Scheduled,
        };
        return (await ClashesOfAsync(probe, cancellationToken))
            .Select(c => new EventClashDto
            {
                EventId = c.Other.Id,
                EventNumber = c.Other.EventNumber,
                EventName = c.Other.EventName,
                When = CompanyEventRules.Describe(EventWindow.Of(c.Other)),
                Audience = CompanyEventRules.DescribeAudience(c.Other, c.Other.OrganizationUnit?.Name),
                SiteName = c.Other.SiteLocation?.Name,
                Refused = c.Kind == EventClash.Refused,
                Message = ClashMessage(probe, c.Other, c.Kind),
            })
            .ToList();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2h — the drill's event (C-51)
    // ═════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>One live event per drill</b>, found by <see cref="CompanyEventRules.DrillSource"/> and the drill's id:
    /// all-day on its next date, at its site, organised by its coordinator, "Emergency drill: {plan}" (cut to the event's
    /// 100 characters), for everyone (the user's ruling) and on the company calendar. Reminders are off — Safety sends its
    /// own "DrillDue" — and no approval is asked.</para>
    ///
    /// <para><b>Never blocked</b> (the user's ruling): no clash is checked, and <see cref="CompanyEventRules.ClashOf"/>
    /// makes any overlap with it a warning. A new next date moves the event, as a reschedule does (anyone invited is told);
    /// no next date, or the drill deleted, cancels it. A date set again after a cancellation makes a new one.</para>
    /// </remarks>
    public async Task SyncDrillEventAsync(DrillEventSyncDto drill, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var existing = await _eventRepository.GetQueryable()
            .Include(e => e.Organizer)
            .Include(e => e.SiteLocation)
            .Where(e => e.TenantId == tenantId && e.SourceEntityType == CompanyEventRules.DrillSource && e.SourceEntityId == drill.DrillId
                        && !e.IsCancelled && e.Status != EventStatus.Cancelled && e.Status != EventStatus.Completed)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var name = $"Emergency drill: {drill.PlanName}".Trim();
        if (name.Length > 100) name = name[..99].TrimEnd() + "…";

        if (drill.Removed || drill.NextDate is null)
        {
            if (existing is null) return;
            var reason = drill.Removed
                ? drill.RemovedReason ?? $"Emergency drill {drill.DrillNumber} was deleted in Safety."
                : $"Emergency drill {drill.DrillNumber} no longer has a next date.";
            existing.IsCancelled = true;
            existing.CancellationDate = DateTime.UtcNow;
            existing.CancellationReason = reason;
            existing.Status = EventStatus.Cancelled;
            existing.CalendarSequence++;
            await CancelLinkedBookingsAsync(existing, $"{existing.EventNumber} was cancelled: {reason}", cancellationToken);
            await _eventRepository.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Drill event {EventNumber} cancelled: {Reason}", existing.EventNumber, reason);
            await NotifyParticipantsAsync(existing, CompanyScheduleEmailCatalog.Events.EventCancelled,
                tokens => { tokens["CancellationReason"] = reason; return tokens; },
                "event cancelled", CompanyScheduleNotices.Cancelled,
                calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken);
            return;
        }

        var day = drill.NextDate.Value.Date;
        if (existing is not null)
        {
            existing.EventName = name;
            existing.LocationId = drill.LocationId;
            existing.OrganizerId = drill.CoordinatorId;
            CompanyEventChangeDto? moved = null;
            if (existing.StartDate.Date != day || existing.EndDate.Date != day)
            {
                var before = EventWindow.Of(existing);
                ApplyWindow(existing, new EventWindow(day, null, day, null, true));
                moved = await MoveAsync(existing, before, $"The next date of emergency drill {drill.DrillNumber} changed in Safety.", cancellationToken);
            }
            await _eventRepository.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (moved is not null)
            {
                _logger.LogInformation("Drill event {EventNumber} moved to {Day:yyyy-MM-dd}", existing.EventNumber, day);
                await NotifyRescheduledAsync(existing, cancellationToken);
            }
            return;
        }

        var created = new CompanyEvent
        {
            TenantId = tenantId,
            EventName = name,
            Description = $"The next emergency drill of {drill.PlanName}, recorded in Safety as drill {drill.DrillNumber}. "
                          + "Its date follows the drill's next date.",
            Category = EventCategory.CompanyEvent,
            Type = EventType.Internal,
            Priority = EventPriority.High,
            StartDate = day,
            EndDate = day,
            IsAllDayEvent = true,
            LocationType = EventLocation.OnSite,
            LocationId = drill.LocationId,
            OrganizerId = drill.CoordinatorId,
            Scope = ParticipantScope.AllStaff,
            Visibility = EventVisibility.Public,
            ShowOnCompanyCalendar = true,
            ShowOnIntranet = false,
            RequiresApproval = false,
            SendReminders = false,
            Status = EventStatus.Scheduled,
            SourceEntityType = CompanyEventRules.DrillSource,
            SourceEntityId = drill.DrillId,
        };
        created.EventNumber = await _eventRepository.GetNextEventNumberAsync(tenantId, cancellationToken);
        StampCreator(created);
        await _eventRepository.AddAsync(created);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Drill event {EventNumber} made for emergency drill {DrillNumber} on {Day:yyyy-MM-dd}",
            created.EventNumber, drill.DrillNumber, day);
    }

    /// <summary>"Emergency drill DRILL-2026-001 — Q1 Fire Evacuation Drill (Head Office plan)", linked to its plan's page.</summary>
    private async Task<EventSourceDto?> SourceOfAsync(CompanyEvent e, CancellationToken cancellationToken)
    {
        if (e.SourceEntityType != CompanyEventRules.DrillSource || e.SourceEntityId is not { } drillId) return null;
        // ⚠ IncludingDeleted, not GetQueryable().IgnoreQueryFilters(): GetQueryable drops deleted rows with a Where of its
        // own, which no filter switch brings back — a deleted drill read as "no longer recorded" (the 2h proof).
        var drill = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Safety.EmergencyDrill>()
            .GetQueryableIncludingDeleted(d => d.Id == drillId && d.TenantId == e.TenantId).AsNoTracking()
            // A drill outlives a deleted plan (the plan's delete leaves its drills), so either one gone means no page to open.
            .Select(d => new { d.DrillNumber, d.DrillName, Gone = d.IsDeleted || d.EmergencyPlan.IsDeleted, d.EmergencyPlanId, d.EmergencyPlan.PlanName })
            .FirstOrDefaultAsync(cancellationToken);
        if (drill is null) return new EventSourceDto { Kind = CompanyEventRules.DrillSource, Label = "Emergency drill (no longer recorded)" };
        return new EventSourceDto
        {
            Kind = CompanyEventRules.DrillSource,
            Label = $"Emergency drill {drill.DrillNumber} — {drill.DrillName} ({drill.PlanName}){(drill.Gone ? ", since deleted" : string.Empty)}",
            Link = drill.Gone ? null : $"/hr/safety/emergency/{drill.EmergencyPlanId}",
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

    /// <summary>Tells everybody invited that the event moved, and from when (round 4, D6) — or those <paramref name="filter"/> lets through.</summary>
    private Task<CompanyEventNoticeResultDto> NotifyRescheduledAsync(
        CompanyEvent entity, CancellationToken cancellationToken, Func<EventParticipant, bool>? filter = null)
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
            "event rescheduled", CompanyScheduleNotices.Rescheduled, filter,
            calendar: HrCalendarMethod.Request, cancellationToken: cancellationToken);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2f-1 — a recurring event is a series (D-2, D-12)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An occurrence of <paramref name="template"/>'s series starting on <paramref name="start"/>: everything an
    /// occurrence shares with its series, the dates and the reply-by date moved by the same amount, and nothing that
    /// belongs to one meeting — no approval, outcome, cost, cancellation, move or sent stamps.
    /// </summary>
    /// <remarks>
    /// Guests, the register, papers and tasks are each occurrence's own (D-12: people miss one week and not the next),
    /// so none is copied. The retired department and a drill's source are not either.
    /// </remarks>
    private static CompanyEvent NewOccurrence(CompanyEvent template, DateTime start, int number)
    {
        var offset = start.Date - template.StartDate.Date;
        return new CompanyEvent
        {
            TenantId = template.TenantId,
            EventName = template.EventName,
            Description = template.Description,
            Category = template.Category,
            Type = template.Type,
            Priority = template.Priority,
            StartDate = start.Date,
            StartTime = template.StartTime,
            EndDate = template.EndDate.Date + offset,
            EndTime = template.EndTime,
            IsAllDayEvent = template.IsAllDayEvent,
            IsRecurring = true,
            RecurrencePattern = template.RecurrencePattern,
            RecurrenceDetails = template.RecurrenceDetails,
            RecurrenceEndDate = template.RecurrenceEndDate,
            RecurrenceCount = template.RecurrenceCount,
            RecurrenceSeriesId = template.RecurrenceSeriesId,
            OccurrenceNumber = number,
            LocationType = template.LocationType,
            VenueName = template.VenueName,
            VenueAddress = template.VenueAddress,
            OnlineMeetingLink = template.OnlineMeetingLink,
            MeetingPassword = template.MeetingPassword,
            LocationId = template.LocationId,
            OrganizerId = template.OrganizerId,
            OrganizationUnitId = template.OrganizationUnitId,
            Scope = template.Scope,
            EstimatedAttendees = template.EstimatedAttendees,
            RequiresRsvp = template.RequiresRsvp,
            RsvpDeadline = template.RsvpDeadline + offset,
            Visibility = template.Visibility,
            ShowOnCompanyCalendar = template.ShowOnCompanyCalendar,
            ShowOnIntranet = template.ShowOnIntranet,
            Status = EventStatus.Scheduled,
            RequiresApproval = template.RequiresApproval,
            HasBudget = template.HasBudget,
            BudgetAmount = template.BudgetAmount,
            BudgetCode = template.BudgetCode,
            RequiredResources = template.RequiredResources,
            CateringRequirements = template.CateringRequirements,
            TechnicalRequirements = template.TechnicalRequirements,
            SendReminders = template.SendReminders,
            ReminderDaysBefore = template.ReminderDaysBefore,
            AdditionalNotes = template.AdditionalNotes,
        };
    }

    /// <summary>
    /// For each event, the day the company does not work it falls on, worded — a public holiday or a company-wide
    /// closure (D-12: generated and flagged, never skipped). Events on ordinary days are not keys.
    /// </summary>
    /// <remarks>
    /// One read of each over the whole span when it is short; event by event when it is long (a yearly series spans
    /// decades, and the holiday read refuses an absurd span). A site's or a unit's closure is not a company day off.
    /// </remarks>
    private async Task<Dictionary<Guid, string>> DayOffNotesAsync(
        Guid tenantId, IReadOnlyCollection<(Guid Id, DateTime Start, DateTime End)> events, CancellationToken cancellationToken)
    {
        var notes = new Dictionary<Guid, string>();
        if (events.Count == 0) return notes;

        var min = events.Min(e => e.Start);
        var max = events.Max(e => e.End);
        var spans = (max - min).TotalDays <= 400
            ? new List<(DateOnly From, DateOnly To)> { (DateOnly.FromDateTime(min), DateOnly.FromDateTime(max)) }
            : events.Select(e => (From: DateOnly.FromDateTime(e.Start), To: DateOnly.FromDateTime(e.End))).ToList();

        var holidays = new List<HrHolidayDay>();
        var closures = new List<BusinessClosure>();
        foreach (var (from, to) in spans)
        {
            holidays.AddRange(await _workingDays.GetHolidaysAsync(tenantId, from, to, cancellationToken));
            closures.AddRange((await _closureCalendar.GetClosuresAsync(tenantId, from, to, cancellationToken))
                .Where(c => BusinessClosureRules.IsNonWorking(c) && BusinessClosureRules.ScopeOf(c).Kind == ClosureScopeKind.Company));
        }

        foreach (var (id, start, end) in events)
        {
            for (var day = DateOnly.FromDateTime(start); day <= DateOnly.FromDateTime(end); day = day.AddDays(1))
            {
                var holiday = holidays.FirstOrDefault(h => h.Date == day);
                var closure = holiday is null ? closures.FirstOrDefault(c => BusinessClosureRules.Covers(c, day)) : null;
                if (holiday is null && closure is null) continue;
                var what = holiday is not null
                    ? $"a public holiday: {holiday.Name}{(holiday.InLieu ? " (the day given in lieu)" : string.Empty)}"
                    : $"a company-wide closure: {closure!.Title}";
                notes[id] = start.Date == end.Date ? $"Falls on {what}." : $"Its {day:dddd, d MMMM} is {what}.";
                break;
            }
        }
        return notes;
    }

    /// <summary>"EVT-2026-00412, Tuesday 1 July 2026: falls on …" — for a save's warnings.</summary>
    private static List<string> DayOffWarnings(IEnumerable<CompanyEvent> events, IReadOnlyDictionary<Guid, string> notes) =>
        events.Where(e => notes.ContainsKey(e.Id))
            .Select(e => $"{e.EventNumber}, {e.StartDate:dddd d MMMM yyyy}: {char.ToLowerInvariant(notes[e.Id][0])}{notes[e.Id][1..]} "
                         + "It is kept; move it if it should not go ahead that day.")
            .ToList();

    /// <summary>How many occurrences each series in <paramref name="dtos"/> has — "occurrence 3 of 10".</summary>
    private async Task FillSeriesCountsAsync(IReadOnlyCollection<CompanyEventDto> dtos, Guid tenantId, CancellationToken cancellationToken)
    {
        var ids = dtos.Where(d => d.RecurrenceSeriesId is not null).Select(d => d.RecurrenceSeriesId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;
        var counts = await _eventRepository.GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.RecurrenceSeriesId != null && ids.Contains(e.RecurrenceSeriesId.Value))
            .GroupBy(e => e.RecurrenceSeriesId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        foreach (var d in dtos)
            if (d.RecurrenceSeriesId is { } s && counts.TryGetValue(s, out var count)) d.OccurrenceCount = count;
    }

    /// <summary>A series' live occurrences, in order — narrow rows, no includes.</summary>
    private Task<List<EventSeriesOccurrenceDto>> SeriesOccurrencesAsync(Guid seriesId, Guid tenantId, CancellationToken cancellationToken) =>
        _eventRepository.GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.RecurrenceSeriesId == seriesId)
            .OrderBy(e => e.OccurrenceNumber)
            .Select(e => new EventSeriesOccurrenceDto
            {
                Id = e.Id,
                EventNumber = e.EventNumber,
                OccurrenceNumber = e.OccurrenceNumber ?? 0,
                StartDate = e.StartDate,
                StartTime = e.StartTime,
                EndDate = e.EndDate,
                Status = e.Status,
                IsCancelled = e.IsCancelled,
            })
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>On the series' rule, counted from its first date</b> — the first occurrence's original date, so a
    /// first occurrence moved on its own does not move the rule — and copied from its LAST occurrence, which carries
    /// the latest edits. The series holds at most 52, deleted occurrences included in the numbering.</para>
    ///
    /// <para>An occurrence on a holiday or a company-wide closure is made and flagged. New occurrences that need
    /// approval are approved together: the first of them goes to the engine, and its decision covers the rest
    /// (the user's ruling).</para>
    ///
    /// <para>Lane 2f-2a (the user's ruling): the latest occurrence's guests are put on the new dates and invited once
    /// each, listing them — or with the approval, when the new dates need one. Their answers start afresh.</para>
    /// </remarks>
    public async Task<EventSeriesResultDto> ExtendSeriesAsync(Guid eventId, ExtendEventSeriesDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        if (ev.RecurrenceSeriesId is not { } seriesId || ev.RecurrencePattern is not { } pattern)
            throw new InvalidOperationException($"{ev.EventName} is a single event, not a series. Make a new recurring event instead.");

        // Deleted occurrences hold their place in the numbering and the rule, as a deleted event holds its number.
        // ⚠ IncludingDeleted (lane 2h's proof): this was GetQueryable().IgnoreQueryFilters(), and GetQueryable drops deleted
        // rows with a Where of its own — a deleted latest occurrence was made again under its own number.
        var all = await _eventRepository.GetQueryableIncludingDeleted(e => e.TenantId == tenantId && e.RecurrenceSeriesId == seriesId)
            .Select(e => new { e.Id, e.OccurrenceNumber, e.StartDate, e.OriginalStartDate, e.IsDeleted })
            .ToListAsync(cancellationToken);
        var anchor = all.Where(x => x.OccurrenceNumber == 1).Select(x => x.OriginalStartDate ?? x.StartDate).FirstOrDefault();
        if (anchor == default) anchor = all.OrderBy(x => x.OccurrenceNumber).First().StartDate;
        var last = all.Max(x => x.OccurrenceNumber ?? 0);
        var templateId = all.Where(x => !x.IsDeleted).OrderByDescending(x => x.OccurrenceNumber).Select(x => x.Id).First();
        var template = templateId == ev.Id ? ev : await GetOwnedEventAsync(templateId, cancellationToken);

        // Lane 2f-2b (finding 3): a series moved together carries its move into what is added — the shift from the rule
        // its latest two consecutive dates share. A date moved on its own differs from its neighbours and is not followed.
        var shifts = all.Where(x => !x.IsDeleted && x.OccurrenceNumber is > 0)
            .OrderBy(x => x.OccurrenceNumber)
            .Select(x => x.StartDate.Date - CompanyEventSeries.DateAt(pattern, anchor, x.OccurrenceNumber!.Value - 1))
            .ToList();
        var shift = TimeSpan.Zero;
        for (var i = shifts.Count - 1; i > 0; i--)
            if (shifts[i] == shifts[i - 1])
            {
                shift = shifts[i];
                break;
            }

        var (total, refusal) = CompanyEventSeries.Plan(pattern, dto.Count, dto.Until is { } until ? until - shift : null, anchor,
            (template.EndDate.Date - template.StartDate.Date).Days, already: last);
        Refuse(refusal);

        var made = new List<CompanyEvent>();
        for (var index = last; index < total; index++)
            made.Add(NewOccurrence(template, CompanyEventSeries.DateAt(pattern, anchor, index) + shift, index + 1));

        // Lane 2g-2 (C-15): a new date the server would refuse beside another refuses the extension, named — before a
        // number is taken.
        foreach (var occurrence in made)
        {
            try
            {
                await RefuseClashAsync(occurrence, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"Occurrence {occurrence.OccurrenceNumber}, {SeriesDateLine(occurrence)}: {ex.Message}", ex);
            }
        }

        foreach (var occurrence in made)
        {
            occurrence.EventNumber = await _eventRepository.GetNextEventNumberAsync(tenantId, cancellationToken);
            StampCreator(occurrence);
            await _eventRepository.AddAsync(occurrence);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Series of {EventNumber} extended by {Count} occurrence(s), to {Total}", ev.EventNumber, made.Count, total);

        // Lane 2f-2a (the user's ruling): the latest occurrence's guests are invited to the new dates — whatever they
        // answered for that one date — bar a leaver (F-35).
        var guests = await TenantGuests(tenantId)
            .Where(p => p.EventId == template.Id)
            .Where(p => p.EmployeeId == null || (p.Employee!.IsActive && !p.Employee!.IsDeleted))
            .ToListAsync(cancellationToken);
        foreach (var occurrence in made)
            foreach (var guest in guests)
                await _participantRepository.AddAsync(CopyGuest(guest, occurrence.Id));
        if (guests.Count > 0)
            await SaveRefusingDuplicateAsync(GuestIndex, "A guest was added to one of the new dates at the same moment. Look again.", cancellationToken);

        // The user's ruling: approved once for what was added — the first new occurrence asks, its decision covers the rest,
        // and its approval sends the guests' invitations (F-33). Otherwise one invitation per guest now.
        CompanyEventNoticeResultDto? told = null;
        if (made[0].RequiresApproval) await StartApprovalAsync(made[0], cancellationToken);
        else if (guests.Count > 0) told = await InviteWaitingAcrossAsync(made, cancellationToken);

        var notes = await DayOffNotesAsync(tenantId, made.Select(m => (m.Id, m.StartDate, m.EndDate)).ToList(), cancellationToken);
        var warnings = DayOffWarnings(made, notes);
        // Lane 2g-2 (C-15): an overlap the server allows is said, by date.
        foreach (var occurrence in made) warnings.AddRange(await ClashWarningsAsync(occurrence, true, cancellationToken));
        return new EventSeriesResultDto
        {
            Guests = guests.Count,
            Told = told,
            Occurrences = made.Select(m => new EventSeriesOccurrenceDto
            {
                Id = m.Id, EventNumber = m.EventNumber, OccurrenceNumber = m.OccurrenceNumber ?? 0,
                StartDate = m.StartDate, StartTime = m.StartTime, EndDate = m.EndDate, Status = m.Status,
                DayOffNote = notes.GetValueOrDefault(m.Id),
            }).ToList(),
            Warnings = warnings,
        };
    }

    /// <summary>
    /// The occurrences of <paramref name="e"/>'s series a decision on <paramref name="e"/> also decides (the user's
    /// ruling: approved once, for the series) — those still awaiting approval with no approval of their own under way.
    /// </summary>
    /// <remarks>
    /// An occurrence moved after its approval was sent back for approval of its own (D-10), and keeps it. The rule is
    /// "every occurrence still waiting with nothing under way of its own", so an approval of a moved occurrence also
    /// covers an extension waiting at the same moment.
    /// </remarks>
    private async Task<List<CompanyEvent>> SharingApprovalAsync(CompanyEvent e, CancellationToken cancellationToken)
    {
        if (e.RecurrenceSeriesId is not { } seriesId) return [];
        var waiting = await _eventRepository.GetQueryable()
            .Include(x => x.Organizer)
            .Include(x => x.SiteLocation) // their invitations' calendar files name the site (lane 2f-2a)
            .Where(x => x.TenantId == e.TenantId && x.RecurrenceSeriesId == seriesId && x.Id != e.Id
                        && x.RequiresApproval && x.ApprovalDate == null && !x.IsCancelled
                        && x.Status != EventStatus.Cancelled && x.Status != EventStatus.Completed)
            .OrderBy(x => x.OccurrenceNumber)
            .ToListAsync(cancellationToken);
        var sharing = new List<CompanyEvent>();
        foreach (var x in waiting)
            if (!await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, x.Id)) sharing.Add(x);
        return sharing;
    }

    /// <summary>
    /// Refuses a decision on an occurrence whose approval is under way on another occurrence of its series (the user's
    /// ruling: approved once) — naming the one to decide.
    /// </summary>
    private async Task EnsureNotSharedElsewhereAsync(CompanyEvent e, string verb, CancellationToken cancellationToken)
    {
        if (e.RecurrenceSeriesId is not { } seriesId) return;
        if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, e.Id)) return;

        var others = await _eventRepository.GetQueryable().AsNoTracking()
            .Where(x => x.TenantId == e.TenantId && x.RecurrenceSeriesId == seriesId && x.Id != e.Id
                        && x.RequiresApproval && x.ApprovalDate == null && !x.IsCancelled
                        && x.Status != EventStatus.Cancelled && x.Status != EventStatus.Completed)
            .OrderBy(x => x.OccurrenceNumber)
            .Select(x => new { x.Id, x.EventNumber, x.OccurrenceNumber })
            .ToListAsync(cancellationToken);
        foreach (var other in others)
            if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, other.Id))
                throw new InvalidOperationException(
                    $"{e.EventName} is approved with its series: {verb} {other.EventNumber} (occurrence {other.OccurrenceNumber}), "
                    + "and the decision covers this occurrence too.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2f-2a — guests and answers across a series (D-12)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The dates of <paramref name="e"/>'s series a series action reaches, in date order — and how many more the scope
    /// covered and left alone because they have started, been completed or been cancelled (D-12: a series action never
    /// changes a past or completed occurrence).
    /// </summary>
    /// <remarks>
    /// "This and following" is by date, as a calendar reads it, from this occurrence on. Each date comes with its
    /// organiser and site, which its calendar file names. <paramref name="e"/> is the tracked instance the context
    /// returns again.
    /// </remarks>
    private async Task<(List<CompanyEvent> Open, int Closed)> SeriesTargetsAsync(
        CompanyEvent e, SeriesScope scope, CancellationToken cancellationToken)
    {
        if (scope == SeriesScope.ThisOccurrence || e.RecurrenceSeriesId is not { } seriesId)
            return (new List<CompanyEvent> { e }, 0);

        var members = await _eventRepository.GetQueryable()
            .Include(x => x.Organizer)
            .Include(x => x.SiteLocation)
            .Where(x => x.TenantId == e.TenantId && x.RecurrenceSeriesId == seriesId)
            .ToListAsync(cancellationToken);
        var covered = members
            .Where(x => scope == SeriesScope.WholeSeries
                        || x.StartDate.Date > e.StartDate.Date
                        || (x.StartDate.Date == e.StartDate.Date && (x.OccurrenceNumber ?? 0) >= (e.OccurrenceNumber ?? 0)))
            .OrderBy(x => x.StartDate).ThenBy(x => x.OccurrenceNumber)
            .ToList();
        var now = DateTime.UtcNow;
        var open = covered.Where(x => !CompanyEventRules.IsClosed(x) && EventWindow.Of(x).Start > now).ToList();
        return (open, covered.Count - open.Count);
    }

    /// <summary>"from this date on" / "in the series" — how a refusal names the scope.</summary>
    private static string ScopeWords(SeriesScope scope) =>
        scope == SeriesScope.WholeSeries ? "in the series" : "from this date on";

    /// <summary>The same guest's rows: an employee by who they are, an outside guest by address, in any case.</summary>
    private static System.Linq.Expressions.Expression<Func<EventParticipant, bool>> SamePerson(Guid? employeeId, string? address)
    {
        if (employeeId is { } id) return p => p.EmployeeId == id;
        var lower = (address ?? string.Empty).ToLower();
        return p => p.EmployeeId == null && p.ExternalParticipantEmail != null && p.ExternalParticipantEmail.ToLower() == lower;
    }

    /// <summary>One person, however many dates they are on — what a series action sends one notice to.</summary>
    private static string PersonKey(EventParticipant p) =>
        p.EmployeeId is { } id ? $"e:{id}" : $"x:{(p.ExternalParticipantEmail ?? p.Id.ToString()).ToLowerInvariant()}";

    /// <summary>A guest on another date of the series: who they are and how they take part, not yet invited.</summary>
    private static EventParticipant CopyGuest(EventParticipant from, Guid eventId) => new()
    {
        TenantId = from.TenantId,
        EventId = eventId,
        EmployeeId = from.EmployeeId,
        ExternalParticipantName = from.ExternalParticipantName,
        ExternalParticipantEmail = from.ExternalParticipantEmail,
        ExternalParticipantOrganization = from.ExternalParticipantOrganization,
        Role = from.Role,
        IsRequired = from.IsRequired,
        SpecialRequirements = from.SpecialRequirements,
        InvitationStatus = InvitationStatus.NotSent,
    };

    /// <summary>"Monday 3 August 2028, 09:00 – 10:00" — a date as a series email lists it.</summary>
    private static string SeriesDateLine(CompanyEvent e)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var days = e.StartDate.ToString("dddd d MMMM yyyy", culture)
                   + (e.EndDate.Date != e.StartDate.Date ? " – " + e.EndDate.ToString("dddd d MMMM yyyy", culture) : string.Empty);
        return !e.IsAllDayEvent && e.StartTime is { } st && e.EndTime is { } et
            ? $@"{days}, {st:hh\:mm} – {et:hh\:mm}"
            : $"{days}, all day";
    }

    /// <summary>
    /// The dates a series email lists, each with its event number — built here, every value encoded, and emitted raw
    /// (<c>{{{SeriesDates}}}</c>, declared HTML on the catalogue).
    /// </summary>
    private static string SeriesDatesHtml(IEnumerable<CompanyEvent> events) =>
        "<ul style='margin:0.5rem 0 1rem;padding-left:1.25rem'>"
        + string.Concat(events.Select(e =>
            $"<li>{System.Net.WebUtility.HtmlEncode(SeriesDateLine(e))} ({System.Net.WebUtility.HtmlEncode(e.EventNumber)})</li>"))
        + "</ul>";

    /// <summary>
    /// Sends one guest ONE invitation to several dates (the user's ruling: one notice per guest per series action) —
    /// by email, listing the dates with a calendar entry for each (none for a postponed one), and in the app — and marks
    /// each of their rows sent once it reached them (lane 2e-2).
    /// </summary>
    /// <remarks>As <see cref="SendInvitationAsync"/>: a leaver is never invited (F-35); inviting oneself counts as
    /// reached and tells nobody. Counted once per person, not per date.</remarks>
    /// <param name="rows">The guest's rows, one per date, in date order, each with its event.</param>
    private async Task<bool> InviteToSeriesAsync(
        IReadOnlyList<(CompanyEvent Ev, EventParticipant Guest)> rows, CompanyEventNoticeResultDto tally,
        CancellationToken cancellationToken)
    {
        var (first, guest) = rows[0];
        if (guest.Employee is { IsActive: false }) return false;
        tally.Issued++;

        var reached = guest.EmployeeId is { } self && self == await _notices.ActorEmployeeIdAsync(cancellationToken);
        if (!reached)
        {
            var name = guest.Employee is not null
                ? $"{guest.Employee.FirstName} {guest.Employee.LastName}".Trim()
                : guest.ExternalParticipantName ?? "Colleague";
            var tokens = EventTokens(first, name);
            tokens["IsRequired"] = guest.IsRequired ? "true" : null;
            tokens["SpecialRequirements"] = guest.SpecialRequirements;
            tokens["SeriesDates"] = SeriesDatesHtml(rows.Select(r => r.Ev));
            tokens["DateCount"] = rows.Count.ToString();
            tokens["SeriesPattern"] = first.RecurrencePattern is { } pattern ? CompanyEventSeries.Describe(pattern) : null;

            var address = guest.Employee?.EmailAddress ?? guest.ExternalParticipantEmail;
            var files = new List<EmailAttachmentDto>();
            if (!string.IsNullOrWhiteSpace(address))
                foreach (var (ev, row) in rows.Where(r => r.Ev.Status != EventStatus.Postponed))
                    files.Add(CalendarFileFor(ev, HrCalendarMethod.Request, await CalendarOrganizerAsync(ev, cancellationToken),
                        address, name, row.IsRequired));
            var emailed = await SendEventEmailWithFilesAsync(
                first.TenantId, CompanyScheduleEmailCatalog.Events.EventSeriesInvitation, address, tokens, "series invitation", files);
            if (emailed) tally.Emailed++;
            else if (!string.IsNullOrWhiteSpace(address)) tally.EmailsNotTaken++;

            var inApp = guest.EmployeeId is { } employeeId
                && (await _notices.TellAsync(first, CompanyScheduleNotices.SeriesInvited, CompanyScheduleNotices.ToGuest, [employeeId],
                    new Dictionary<string, object> { ["Count"] = rows.Count.ToString() }, cancellationToken: cancellationToken))
                    .Contains(employeeId);
            if (inApp) tally.ToldInApp++;
            reached = emailed || inApp;
        }

        if (!reached)
        {
            _logger.LogInformation("The series invitation to {Count} date(s) of {EventName} reached nobody for {Guest}: left not delivered.",
                rows.Count, first.EventName, GuestName(guest));
            return false;
        }
        tally.Reached++;
        var sentAt = DateTime.UtcNow;
        foreach (var (_, row) in rows)
        {
            // An answer already given stands, as for a single date.
            if (row.InvitationStatus == InvitationStatus.NotSent) row.InvitationStatus = InvitationStatus.Sent;
            row.InvitationSentDate = sentAt;
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Tells one guest ONCE that several dates changed for them together (the user's ruling) — by the series email,
    /// listing the dates with each one's updated calendar entry or its cancellation, and in the app. Never of their own
    /// act, and not a leaver.
    /// </summary>
    /// <param name="rows">The guest's rows, one per date, in date order, each with its event (its sequence already raised).</param>
    /// <param name="title">The change in a few words ("No longer invited").</param>
    /// <param name="summary">The change in a sentence.</param>
    private async Task<CompanyEventNoticeResultDto> TellSeriesChangeAsync(
        IReadOnlyList<(CompanyEvent Ev, EventParticipant Guest)> rows, HrCalendarMethod? method, string title, string summary,
        string? reason, bool nothingRequired, CancellationToken cancellationToken)
    {
        var (first, guest) = rows[0];
        var tally = new CompanyEventNoticeResultDto { MailServerSetUp = await MailServerSetUpAsync(first.TenantId, cancellationToken) };
        if (guest.Employee is { IsActive: false }) return tally;
        if (guest.EmployeeId is { } self && self == await _notices.ActorEmployeeIdAsync(cancellationToken)) return tally;
        tally.Issued = 1;

        var name = guest.Employee is not null
            ? $"{guest.Employee.FirstName} {guest.Employee.LastName}".Trim()
            : guest.ExternalParticipantName ?? "Colleague";
        var tokens = EventTokens(first, name);
        tokens["SeriesDates"] = SeriesDatesHtml(rows.Select(r => r.Ev));
        tokens["DateCount"] = rows.Count.ToString();
        tokens["ChangeTitle"] = title;
        tokens["ChangeSummary"] = summary;
        tokens["Reason"] = reason;
        tokens["NothingRequired"] = nothingRequired ? "true" : null;

        var address = guest.Employee?.EmailAddress ?? guest.ExternalParticipantEmail;
        var files = new List<EmailAttachmentDto>();
        if (method is { } calendar && !string.IsNullOrWhiteSpace(address))
            // A postponed date's entry was taken away; an update must not put it back at the old date (2e-3).
            foreach (var (ev, row) in rows.Where(r => !(calendar == HrCalendarMethod.Request && r.Ev.Status == EventStatus.Postponed)))
                files.Add(CalendarFileFor(ev, calendar, await CalendarOrganizerAsync(ev, cancellationToken), address, name, row.IsRequired));
        var emailed = await SendEventEmailWithFilesAsync(
            first.TenantId, CompanyScheduleEmailCatalog.Events.EventSeriesChanged, address, tokens, "series changed", files);
        if (emailed) tally.Emailed = 1;
        else if (!string.IsNullOrWhiteSpace(address)) tally.EmailsNotTaken = 1;

        var inApp = guest.EmployeeId is { } employeeId
            && (await _notices.TellAsync(first, CompanyScheduleNotices.SeriesChanged, CompanyScheduleNotices.ToGuest, [employeeId],
                new Dictionary<string, object> { ["What"] = title, ["Count"] = rows.Count.ToString() }, cancellationToken: cancellationToken))
                .Contains(employeeId);
        if (inApp) tally.ToldInApp = 1;
        tally.Reached = emailed || inApp ? 1 : 0;
        return tally;
    }

    /// <summary>
    /// Sends the invitations not yet delivered on several dates of a series at once — ONE per guest (the user's ruling):
    /// the single-date invitation to a guest waiting on one date, the series invitation to one waiting on several. Used
    /// when an approval covers a series, and for the dates an extension adds.
    /// </summary>
    private async Task<CompanyEventNoticeResultDto> InviteWaitingAcrossAsync(
        IReadOnlyList<CompanyEvent> events, CancellationToken cancellationToken)
    {
        var tenantId = events[0].TenantId;
        var byId = events.ToDictionary(e => e.Id);
        var ids = byId.Keys.ToList();
        var waiting = await TenantGuests(tenantId)
            .Where(p => ids.Contains(p.EventId) && p.InvitationStatus == InvitationStatus.NotSent)
            .Where(p => p.EmployeeId == null || (p.Employee!.IsActive && !p.Employee!.IsDeleted))
            .ToListAsync(cancellationToken);

        var result = new CompanyEventNoticeResultDto { MailServerSetUp = await MailServerSetUpAsync(tenantId, cancellationToken) };
        foreach (var person in waiting.GroupBy(PersonKey))
        {
            var rows = person.Select(p => (Ev: byId[p.EventId], Guest: p))
                .OrderBy(r => r.Ev.StartDate).ThenBy(r => r.Ev.OccurrenceNumber).ToList();
            if (rows.Count == 1) await InviteAsync(rows[0].Ev, rows[0].Guest, result, cancellationToken);
            else await InviteToSeriesAsync(rows, result, cancellationToken);
        }
        return result;
    }

    /// <summary>
    /// Adds a guest to several dates of a series at once (lane 2f-2a, D-12): every date the scope reaches that is still to
    /// come and that they are not already on, then ONE invitation for the dates that are not awaiting approval — those
    /// wait for it (F-33).
    /// </summary>
    private async Task<EventParticipantDto> AddSeriesGuestAsync(
        CompanyEvent ev, CreateEventParticipantDto createDto, Guid tenantId, CancellationToken cancellationToken)
    {
        var (targets, closed) = await SeriesTargetsAsync(ev, createDto.Scope, cancellationToken);
        if (targets.Count == 0)
            throw new InvalidOperationException(
                $"No date of {ev.EventName} {ScopeWords(createDto.Scope)} is still to come, so nobody can be invited to it.");

        var guest = createDto.ToEntity();
        if (guest.EmployeeId == Guid.Empty) guest.EmployeeId = null;
        guest.TenantId = tenantId;
        // The guest's own checks, once: an outside guest's name and address, a leaver refused (F-35). A date they are
        // already on is passed over below, not refused.
        await CheckGuestAsync(targets[0], guest, isNew: true, cancellationToken, refuseTwice: false);
        var who = guest.EmployeeId is { } employeeId
            ? (await FindEmployeeAsync(employeeId, tenantId, cancellationToken))?.Name ?? "That employee"
            : guest.ExternalParticipantName ?? guest.ExternalParticipantEmail ?? "That guest";

        var targetIds = targets.Select(t => t.Id).ToList();
        var already = (await TenantGuests(tenantId)
                .Where(p => targetIds.Contains(p.EventId))
                .Where(SamePerson(guest.EmployeeId, guest.ExternalParticipantEmail))
                .Select(p => p.EventId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var dates = targets.Where(t => !already.Contains(t.Id)).ToList();
        if (dates.Count == 0)
            throw new InvalidOperationException(
                $"{who} is already invited to every date of {ev.EventName} {ScopeWords(createDto.Scope)} still to come.");

        var added = new List<EventParticipant>();
        foreach (var date in dates)
        {
            var row = CopyGuest(guest, date.Id);
            await _participantRepository.AddAsync(row);
            added.Add(row);
        }
        await SaveRefusingDuplicateAsync(GuestIndex, $"{who} was invited to one of these dates of {ev.EventName} at the same moment. Look again.",
            cancellationToken);

        var addedIds = added.Select(a => a.Id).ToList();
        var saved = await TenantGuests(tenantId).Where(p => addedIds.Contains(p.Id)).ToListAsync(cancellationToken);
        var byId = dates.ToDictionary(d => d.Id);
        var rows = saved.Select(p => (Ev: byId[p.EventId], Guest: p))
            .OrderBy(r => r.Ev.StartDate).ThenBy(r => r.Ev.OccurrenceNumber).ToList();
        _logger.LogInformation("Guest {Guest} invited to {Count} date(s) of {EventName} ({Scope})",
            who, rows.Count, ev.EventName, createDto.Scope);

        // F-33: a date awaiting approval invites nobody yet; its approval sends the invitation.
        var now = rows.Where(r => !CompanyEventRules.IsAwaitingApproval(r.Ev)).ToList();
        var result = new EventSeriesGuestResultDto
        {
            EventNumbers = rows.Select(r => r.Ev.EventNumber).ToList(),
            Skipped = already.Count,
            Closed = closed,
            Waiting = rows.Count - now.Count,
        };
        if (now.Count > 0)
        {
            var told = new CompanyEventNoticeResultDto { MailServerSetUp = await MailServerSetUpAsync(tenantId, cancellationToken) };
            if (now.Count == 1) await InviteAsync(now[0].Ev, now[0].Guest, told, cancellationToken);
            else await InviteToSeriesAsync(now, told, cancellationToken);
            result.Told = told;
        }

        // The answer is this date's row when it was added, else the first date's.
        var shown = rows.FirstOrDefault(r => r.Ev.Id == ev.Id).Guest ?? rows[0].Guest;
        var dto = (await TenantGuests(tenantId).FirstAsync(p => p.Id == shown.Id, cancellationToken)).ToDto();
        dto.Series = result;
        return dto;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lane 2f-2b — edit, move and cancel across a series (D-12)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tells the guests of several dates what happened to them, ONCE each (the user's ruling): the single-date notice
    /// (<paramref name="single"/>, for that guest alone) to a guest on one of the dates, the series email listing the
    /// dates to a guest on several. A change goes to the guests who were invited, never to whoever made it (lane 2e-1).
    /// </summary>
    private async Task<CompanyEventNoticeResultDto> TellAcrossAsync(
        IReadOnlyList<CompanyEvent> events,
        Func<CompanyEvent, Func<EventParticipant, bool>, Task<CompanyEventNoticeResultDto?>> single,
        HrCalendarMethod? method, string title, string summary, string? reason, bool nothingRequired,
        CancellationToken cancellationToken)
    {
        var tally = new CompanyEventNoticeResultDto();
        if (events.Count == 0) return tally;
        var tenantId = events[0].TenantId;
        tally.MailServerSetUp = await MailServerSetUpAsync(tenantId, cancellationToken);

        var actor = await _notices.ActorEmployeeIdAsync(cancellationToken);
        var byId = events.ToDictionary(e => e.Id);
        var ids = byId.Keys.ToList();
        var guests = await TenantGuests(tenantId)
            .Where(p => ids.Contains(p.EventId) && p.InvitationStatus != InvitationStatus.NotSent)
            .Where(p => p.EmployeeId == null || (p.Employee!.IsActive && !p.Employee!.IsDeleted))
            .ToListAsync(cancellationToken);

        foreach (var person in guests.Where(p => actor is null || p.EmployeeId != actor).GroupBy(PersonKey))
        {
            var rows = person.Select(p => (Ev: byId[p.EventId], Guest: p))
                .OrderBy(r => r.Ev.StartDate).ThenBy(r => r.Ev.OccurrenceNumber).ToList();
            if (rows.Count == 1)
            {
                var only = rows[0].Guest.Id;
                if (await single(rows[0].Ev, p => p.Id == only) is { } told) tally.Add(told);
            }
            else tally.Add(await TellSeriesChangeAsync(rows, method, title, summary, reason, nothingRequired, cancellationToken));
        }
        return tally;
    }

    /// <summary>An event's editable fields as they stand — what an edit with a series scope is measured against.</summary>
    private static UpdateCompanyEventDto SnapshotOf(CompanyEvent e) => new()
    {
        Id = e.Id,
        EventName = e.EventName,
        Description = e.Description,
        Category = e.Category,
        Type = e.Type,
        Priority = e.Priority,
        StartDate = e.StartDate,
        StartTime = e.StartTime,
        EndDate = e.EndDate,
        EndTime = e.EndTime,
        IsAllDayEvent = e.IsAllDayEvent,
        LocationType = e.LocationType,
        VenueName = e.VenueName,
        VenueAddress = e.VenueAddress,
        OnlineMeetingLink = e.OnlineMeetingLink,
        MeetingPassword = e.MeetingPassword,
        LocationId = e.LocationId,
        DepartmentId = e.DepartmentId,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizerId = e.OrganizerId,
        Scope = e.Scope,
        EstimatedAttendees = e.EstimatedAttendees,
        RequiresRsvp = e.RequiresRsvp,
        RsvpDeadline = e.RsvpDeadline,
        Visibility = e.Visibility,
        ShowOnCompanyCalendar = e.ShowOnCompanyCalendar,
        ShowOnIntranet = e.ShowOnIntranet,
        Status = e.Status,
        HasBudget = e.HasBudget,
        BudgetAmount = e.BudgetAmount,
        ActualCost = e.ActualCost,
        BudgetCode = e.BudgetCode,
        RequiredResources = e.RequiredResources,
        CateringRequirements = e.CateringRequirements,
        TechnicalRequirements = e.TechnicalRequirements,
        SendReminders = e.SendReminders,
        ReminderDaysBefore = e.ReminderDaysBefore,
        AdditionalNotes = e.AdditionalNotes,
    };

    /// <summary>
    /// The fields an edit carries field for field — everything but the window and the reply-by date, which move by the
    /// edit's offset, and what only steers the edit.
    /// </summary>
    private static readonly System.Reflection.PropertyInfo[] PlainEditFields = typeof(UpdateCompanyEventDto).GetProperties()
        .Where(p => p.CanRead && p.CanWrite && !new[]
        {
            nameof(UpdateCompanyEventDto.Id), nameof(UpdateCompanyEventDto.RescheduleReason), nameof(UpdateCompanyEventDto.SeriesScope),
            nameof(UpdateCompanyEventDto.StartDate), nameof(UpdateCompanyEventDto.StartTime), nameof(UpdateCompanyEventDto.EndDate),
            nameof(UpdateCompanyEventDto.EndTime), nameof(UpdateCompanyEventDto.IsAllDayEvent), nameof(UpdateCompanyEventDto.RsvpDeadline),
        }.Contains(p.Name))
        .ToArray();

    /// <summary>Two field values the same — an empty text and none are.</summary>
    private static bool SameValue(object? a, object? b) =>
        a is string || b is string
            ? string.Equals((a as string)?.Trim() ?? string.Empty, (b as string)?.Trim() ?? string.Empty, StringComparison.Ordinal)
            : Equals(a, b);

    /// <summary>
    /// Edits several dates of a series at once (lane 2f-2b, D-12). Each date takes ONLY what this edit changed on the
    /// date it was made from (the user's ruling): a difference set on one date on purpose is kept. A new window moves
    /// each by the same number of days, to the new times when they changed; a new reply-by date keeps its distance from
    /// each date's start. Each date is checked and applied before anything is saved, so one refused date refuses all —
    /// named. Each guest is told once per kind of change; a moved approved series is approved once (finding 4).
    /// </summary>
    private async Task<CompanyEventDto> UpdateSeriesAsync(
        CompanyEvent acted, UpdateCompanyEventDto dto, Guid tenantId, CancellationToken cancellationToken)
    {
        var (targets, closed) = await SeriesTargetsAsync(acted, dto.SeriesScope, cancellationToken);
        if (targets.Count == 0)
            throw new InvalidOperationException(
                $"No date of {acted.EventName} {ScopeWords(dto.SeriesScope)} is still to come, so none can be changed.");

        // What this edit changes, measured against the date it was made from.
        var before = SnapshotOf(acted);
        var changed = PlainEditFields.Where(p => !SameValue(p.GetValue(before), p.GetValue(dto))).ToList();
        var shift = dto.StartDate.Date - acted.StartDate.Date;
        var span = dto.EndDate.Date - dto.StartDate.Date;
        var timesChanged = dto.StartTime != acted.StartTime || dto.EndTime != acted.EndTime || dto.IsAllDayEvent != acted.IsAllDayEvent;
        var windowChanged = shift != TimeSpan.Zero || span != (acted.EndDate.Date - acted.StartDate.Date) || timesChanged;
        var deadlineChanged = dto.RsvpDeadline != acted.RsvpDeadline;

        var applied = new List<(CompanyEvent Ev, EditOutcome Outcome)>();
        foreach (var target in targets)
        {
            var request = SnapshotOf(target);
            foreach (var field in changed) field.SetValue(request, field.GetValue(dto));
            if (windowChanged)
            {
                request.StartDate = target.StartDate.Date + shift;
                request.EndDate = request.StartDate + span;
                if (timesChanged)
                {
                    request.StartTime = dto.StartTime;
                    request.EndTime = dto.EndTime;
                    request.IsAllDayEvent = dto.IsAllDayEvent;
                }
                request.RescheduleReason = dto.RescheduleReason;
            }
            if (deadlineChanged)
                request.RsvpDeadline = dto.RsvpDeadline is { } deadline ? request.StartDate.Date + (deadline - dto.StartDate.Date) : null;

            try
            {
                applied.Add((target, await ApplyEditAsync(target, request, tenantId, cancellationToken)));
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"{target.EventNumber}, {SeriesDateLine(target)}: {ex.Message} Nothing was changed.", ex);
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Company event series edited from {EventNumber}: {Count} date(s) ({Scope}), {Fields}",
            acted.EventNumber, applied.Count, dto.SeriesScope, string.Join(", ", changed.Select(c => c.Name)));

        // Each guest told once per kind of change.
        var outcomeOf = applied.ToDictionary(a => a.Ev.Id, a => a.Outcome);
        Task<CompanyEventNoticeResultDto?> Single(CompanyEvent ev, Func<EventParticipant, bool> filter) =>
            TellEditAsync(ev, outcomeOf[ev.Id], cancellationToken, filter);
        var told = new CompanyEventNoticeResultDto { MailServerSetUp = await MailServerSetUpAsync(tenantId, cancellationToken) };
        var name = acted.EventName;
        var movedDates = applied.Where(a => a.Outcome.Moving).Select(a => a.Ev).ToList();
        if (movedDates.Count > 0)
            told.Add(await TellAcrossAsync(movedDates, Single, HrCalendarMethod.Request, "Moved",
                $"These dates of {name} have moved; they are now as listed. If you had answered, please answer again for the new times.",
                dto.RescheduleReason?.Trim(), nothingRequired: false, cancellationToken));
        var postponed = applied.Where(a => a.Outcome.PostponedNow).Select(a => a.Ev).ToList();
        if (postponed.Count > 0)
            told.Add(await TellAcrossAsync(postponed, Single, HrCalendarMethod.Cancel, "Postponed",
                $"These dates of {name} are postponed. New dates will follow.", reason: null, nothingRequired: true, cancellationToken));
        foreach (var group in applied.Where(a => a.Outcome.Changed is not null).GroupBy(a => a.Outcome.Changed!.Value.What))
        {
            var sample = group.First();
            var details = new List<string>();
            if (group.Key.Contains("venue", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sample.Ev.VenueName))
                details.Add($"now {sample.Ev.VenueName}{(sample.Outcome.Changed!.Value.SiteName is { } site ? $", {site}" : string.Empty)}");
            if (group.Key.Contains("link", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sample.Ev.OnlineMeetingLink))
                details.Add($"join at {sample.Ev.OnlineMeetingLink}");
            told.Add(await TellAcrossAsync(group.Select(a => a.Ev).ToList(), Single, HrCalendarMethod.Request, "Changed",
                $"{group.Key} for these dates of {name} has changed{(details.Count > 0 ? ": " + string.Join("; ", details) : string.Empty)}.",
                reason: null, nothingRequired: false, cancellationToken));
        }

        // D-10 with the user's ruling (finding 4): a moved approved series is approved afresh, ONCE — the first date
        // that lost its approval asks, and its decision covers the rest.
        if (applied.Where(a => a.Outcome.Moved?.ApprovalCleared == true).Select(a => a.Ev).FirstOrDefault() is { } asks)
            await StartApprovalAsync(asks, cancellationToken);

        var updated = await GetByIdAsync(acted.Id, cancellationToken);
        var anyTold = told.Issued > 0 ? told : null;
        updated.Told = anyTold;
        updated.Series = new EventSeriesChangeResultDto
        {
            EventNumbers = applied.Select(a => a.Ev.EventNumber).ToList(),
            Closed = closed,
            Told = anyTold,
        };
        if (await AudienceWarningAsync(acted, cancellationToken) is { } warning) updated.Warnings.Add(warning);
        // Lane 2g-2 (C-15): an overlap the server allows is said, by date.
        foreach (var (ev, _) in applied.Where(a => a.Outcome.ClashChecked))
            updated.Warnings.AddRange(await ClashWarningsAsync(ev, true, cancellationToken));
        return updated;
    }

    /// <summary>
    /// Moves several dates of a series at once (lane 2f-2b, D-12): each by the same number of days as the date it was
    /// asked from, to the new times when given (each keeps its own when not); a new reply-by date keeps its distance from
    /// each date's start. One refused date refuses all, named. Each guest is told once; a moved approved series is
    /// approved once (finding 4).
    /// </summary>
    private async Task<CompanyEventChangeDto> RescheduleSeriesAsync(
        CompanyEvent acted, RescheduleEventDto dto, CancellationToken cancellationToken)
    {
        var (targets, closed) = await SeriesTargetsAsync(acted, dto.SeriesScope, cancellationToken);
        if (targets.Count == 0)
            throw new InvalidOperationException(
                $"No date of {acted.EventName} {ScopeWords(dto.SeriesScope)} is still to come, so none can be moved.");

        var shift = dto.NewStartDate.Date - acted.StartDate.Date;
        var span = dto.NewEndDate.Date - dto.NewStartDate.Date;
        var keepHours = dto.NewStartTime is null && dto.NewEndTime is null;
        var reason = dto.RescheduleReason.Trim();

        var moved = new List<(CompanyEvent Ev, CompanyEventChangeDto Change)>();
        foreach (var target in targets)
        {
            var before = EventWindow.Of(target);
            var start = target.StartDate.Date + shift;
            var requested = new EventWindow(start, keepHours ? target.StartTime : dto.NewStartTime,
                start + span, keepHours ? target.EndTime : dto.NewEndTime, target.IsAllDayEvent);
            if (requested.SameAs(before)) continue;
            try
            {
                if (dto.NewRsvpDeadline is { } deadline) target.RsvpDeadline = start + (deadline - dto.NewStartDate.Date);
                ValidateWindow(target, requested);
                // Lane 2g-2 (C-15): its new time must not clash where the server refuses it.
                await RefuseClashAsync(target, cancellationToken);
                moved.Add((target, await MoveAsync(target, before, reason, cancellationToken)));
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"{target.EventNumber}, {SeriesDateLine(target)}: {ex.Message} Nothing was moved.", ex);
            }
            await _eventRepository.UpdateAsync(target);
        }
        if (moved.Count == 0)
            throw new InvalidOperationException($"Every date chosen of {acted.EventName} is already there. Choose a different time.");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Company event series moved from {EventNumber}: {Count} date(s) ({Scope}) by {Days} day(s)",
            acted.EventNumber, moved.Count, dto.SeriesScope, shift.Days);

        var told = await TellAcrossAsync(moved.Select(m => m.Ev).ToList(),
            async (ev, filter) => await NotifyRescheduledAsync(ev, cancellationToken, filter),
            HrCalendarMethod.Request, "Moved",
            $"These dates of {acted.EventName} have moved; they are now as listed. If you had answered, please answer again for the new times.",
            reason, nothingRequired: false, cancellationToken);
        // D-10 with the user's ruling (finding 4): approved afresh, once.
        if (moved.Where(m => m.Change.ApprovalCleared).Select(m => m.Ev).FirstOrDefault() is { } asks)
            await StartApprovalAsync(asks, cancellationToken);

        var anyTold = told.Issued > 0 ? told : null;
        var warnings = new List<string>();
        foreach (var (ev, _) in moved) warnings.AddRange(await ClashWarningsAsync(ev, true, cancellationToken));
        return new CompanyEventChangeDto
        {
            Event = await GetByIdAsync(acted.Id, cancellationToken),
            BookingsMoved = moved.SelectMany(m => m.Change.BookingsMoved).ToList(),
            AnswersReset = moved.Sum(m => m.Change.AnswersReset),
            ApprovalCleared = moved.Any(m => m.Change.ApprovalCleared),
            Told = anyTold,
            Series = new EventSeriesChangeResultDto
            {
                EventNumbers = moved.Select(m => m.Ev.EventNumber).ToList(),
                Closed = closed,
                Told = anyTold,
            },
            // Lane 2g-2 (C-15): an overlap the server allows is said, by date.
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Cancels several dates of a series at once (lane 2f-2b, D-12) — "this and following" ends the series there. Their
    /// rooms are cancelled with them and their approvals withdrawn; the series' approval passes on when a cancelled date
    /// carried it (finding 2). Each guest is told once, with each date's calendar entry cancelled.
    /// </summary>
    private async Task<CompanyEventChangeDto> CancelSeriesAsync(
        CompanyEvent acted, CancelEventDto dto, CancellationToken cancellationToken)
    {
        var (targets, closed) = await SeriesTargetsAsync(acted, dto.SeriesScope, cancellationToken);
        if (targets.Count == 0)
            throw new InvalidOperationException(
                $"No date of {acted.EventName} {ScopeWords(dto.SeriesScope)} is still to come, so none can be cancelled.");

        var reason = dto.CancellationReason.Trim();
        var bookings = new List<string>();
        var cancelledAt = DateTime.UtcNow;
        foreach (var target in targets)
        {
            target.IsCancelled = true;
            target.CancellationDate = cancelledAt;
            target.CancellationReason = reason;
            target.Status = EventStatus.Cancelled;
            // Lane 2e-3 (D-14): the cancellation takes the guests' calendar entries away.
            target.CalendarSequence++;
            bookings.AddRange(await CancelLinkedBookingsAsync(target, $"{target.EventNumber} was cancelled: {reason}", cancellationToken));
            await _eventRepository.UpdateAsync(target);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Company event series cancelled from {EventNumber}: {Count} date(s) ({Scope}), {Bookings} room booking(s) with them",
            acted.EventNumber, targets.Count, dto.SeriesScope, bookings.Count);

        // D-10: nothing left to approve on them — and the series' approval passes on if one of them carried it.
        var carried = false;
        foreach (var target in targets)
            carried |= await CancelApprovalAsync(target, $"The event was cancelled: {reason}");
        if (carried && acted.RecurrenceSeriesId is { } seriesId)
            await PassSeriesApprovalOnAsync(acted.TenantId, seriesId, cancellationToken);

        var told = await TellAcrossAsync(targets,
            async (ev, filter) => await NotifyParticipantsAsync(ev, CompanyScheduleEmailCatalog.Events.EventCancelled,
                tokens =>
                {
                    tokens["CancellationReason"] = ev.CancellationReason;
                    return tokens;
                },
                "event cancelled", CompanyScheduleNotices.Cancelled, filter,
                calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken),
            HrCalendarMethod.Cancel, "Cancelled", $"These dates of {acted.EventName} have been cancelled.",
            reason, nothingRequired: true, cancellationToken);

        var anyTold = told.Issued > 0 ? told : null;
        return new CompanyEventChangeDto
        {
            Event = await GetByIdAsync(acted.Id, cancellationToken),
            BookingsCancelled = bookings,
            Told = anyTold,
            Series = new EventSeriesChangeResultDto
            {
                EventNumbers = targets.Select(t => t.EventNumber).ToList(),
                Closed = closed,
                Told = anyTold,
            },
        };
    }

    /// <summary>
    /// When the date carrying a series' approval is cancelled or deleted (finding 2), the approval passes to the next
    /// date still waiting — so the rest are not left with nothing under way, which nobody could decide while a
    /// definition is published. Nothing happens when another approval is already under way, or nothing waits.
    /// </summary>
    private async Task PassSeriesApprovalOnAsync(Guid tenantId, Guid seriesId, CancellationToken cancellationToken)
    {
        var waiting = await _eventRepository.GetQueryable()
            .Include(x => x.Organizer)
            .Where(x => x.TenantId == tenantId && x.RecurrenceSeriesId == seriesId
                        && x.RequiresApproval && x.ApprovalDate == null && !x.IsCancelled
                        && x.Status != EventStatus.Cancelled && x.Status != EventStatus.Completed)
            .OrderBy(x => x.StartDate).ThenBy(x => x.OccurrenceNumber)
            .ToListAsync(cancellationToken);
        if (waiting.Count == 0) return;
        foreach (var x in waiting)
            if (await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, x.Id)) return;

        await StartApprovalAsync(waiting[0], cancellationToken);
        _logger.LogInformation("The series' approval passed to {EventNumber}, the next date waiting", waiting[0].EventNumber);
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
    /// <returns>Whether one was under way — for a series, it may have been the series' approval (lane 2f-2b).</returns>
    private async Task<bool> CancelApprovalAsync(CompanyEvent e, string reason)
    {
        try
        {
            if (!await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, e.Id)) return false;
            await _workflow.CancelWorkflowAsync(WorkflowEntityType, e.Id, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not withdraw the approval of event {EventNumber}.", e.EventNumber);
            return false;
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

        // Lane 2f-1 (D-2, D-12): a recurring event is a series, and every occurrence is made now — each a full event.
        // It used to store "repeats weekly, 10 times" and make nothing (C-14).
        var occurrences = 1;
        if (entity.IsRecurring)
        {
            var (count, refusal) = CompanyEventSeries.Plan(entity.RecurrencePattern, entity.RecurrenceCount,
                entity.RecurrenceEndDate, entity.StartDate, (entity.EndDate.Date - entity.StartDate.Date).Days);
            Refuse(refusal);
            occurrences = count;
            entity.RecurrenceSeriesId = Guid.NewGuid();
            entity.OccurrenceNumber = 1;
        }
        else
        {
            entity.RecurrencePattern = null;
            entity.RecurrenceCount = null;
            entity.RecurrenceEndDate = null;
            entity.RecurrenceDetails = null;
        }

        entity.Status = EventStatus.Scheduled;
        var series = new List<CompanyEvent> { entity };
        for (var index = 1; index < occurrences; index++)
            series.Add(NewOccurrence(entity, CompanyEventSeries.DateAt(entity.RecurrencePattern!.Value, entity.StartDate, index), index + 1));

        // Lane 2g-2 (C-15): a clash the server does not allow refuses the event — any date of a series, named — before a
        // number is taken.
        foreach (var occurrence in series)
        {
            try
            {
                await RefuseClashAsync(occurrence, cancellationToken);
            }
            catch (InvalidOperationException ex) when (series.Count > 1)
            {
                throw new InvalidOperationException($"Occurrence {occurrence.OccurrenceNumber}, {SeriesDateLine(occurrence)}: {ex.Message}", ex);
            }
        }

        foreach (var occurrence in series)
        {
            occurrence.EventNumber = await _eventRepository.GetNextEventNumberAsync(tenantId, cancellationToken);
            StampCreator(occurrence);
            await _eventRepository.AddAsync(occurrence);
        }
        // One save: a series is made whole or not at all.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event created: {EventNumber}{Series}", entity.EventNumber,
            occurrences > 1 ? $" and {occurrences - 1} more occurrence(s), to {series[^1].EventNumber}" : string.Empty);

        // D-10: an event that needs approval goes to the engine now — events have no draft to submit. A series is
        // approved once (the user's ruling, 2f-1): its first occurrence asks, and the decision covers the rest.
        if (entity.RequiresApproval) await StartApprovalAsync(entity, cancellationToken);

        // ⚠ Re-read before mapping. `entity` is the graph we just inserted: its Organizer,
        // Department and SiteLocation navigations are still null, so mapping it straight to a DTO
        // answers organizerName "" and locationName null. The caller cannot tell that from real
        // missing data, and any screen that renders the create response shows blanks.
        var created = await GetByIdAsync(entity.Id, cancellationToken);
        // D-16: an audience that reaches nobody is said, not refused — the guests can still be invited.
        if (await AudienceWarningAsync(entity, cancellationToken) is { } warning) created.Warnings.Add(warning);
        // D-12: an occurrence on a day the company does not work is made and flagged, not skipped.
        created.Warnings.AddRange(DayOffWarnings(series,
            await DayOffNotesAsync(tenantId, series.Select(s => (s.Id, s.StartDate, s.EndDate)).ToList(), cancellationToken)));
        // Lane 2g-2 (C-15): an overlap the server allows is said.
        foreach (var occurrence in series)
            created.Warnings.AddRange(await ClashWarningsAsync(occurrence, series.Count > 1, cancellationToken));
        return created;
    }

    /// <summary>
    /// What an edit did to one event — applied, not yet saved, nobody told (lane 2f-2b). <paramref name="ClashChecked"/>:
    /// it changed what a clash depends on (lane 2g-2), so its allowed overlaps are worth saying.
    /// </summary>
    private sealed record EditOutcome(
        bool Moving, CompanyEventChangeDto? Moved, bool PostponedNow, (string What, string? SiteName)? Changed, bool ClashChecked = false);

    /// <summary>
    /// Applies an edit to one event — its checks, a move when the window changes, and what its guests would hear of —
    /// without saving or telling anyone: the edit saves and tells, and a series edit (lane 2f-2b) applies it to each of
    /// its dates first, so one refused date refuses them all.
    /// </summary>
    private async Task<EditOutcome> ApplyEditAsync(
        CompanyEvent entity, UpdateCompanyEventDto updateDto, Guid tenantId, CancellationToken cancellationToken)
    {
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
        // Lane 2g-2 (C-15): what a clash depends on — besides the window — before the edit.
        var audienceBefore = (entity.Scope, entity.Visibility, entity.OrganizationUnitId);
        var liveBefore = CompanyEventRules.IsLive(entity);

        if (moving && string.IsNullOrWhiteSpace(updateDto.RescheduleReason))
            throw new InvalidOperationException(
                "Changing the dates or times moves the event, and everybody invited is told why. Give the reason for the change.");

        updateDto.UpdateEntity(entity);
        if (organiserChanged) entity.OrganizerId = updateDto.OrganizerId!.Value;
        if (updateDto.Status is { } status && status != entity.Status) ApplyStatus(entity, status);

        await ValidateAsync(entity, moving ? requested : before, tenantId, organiserChanged, cancellationToken);

        // Lane 2g-2 (C-15): only an edit that changes what a clash depends on is checked — an event already beside another
        // (made before the rule) can still have its description corrected.
        var clashChecked = moving || siteBefore != entity.LocationId
                           || audienceBefore != (entity.Scope, entity.Visibility, entity.OrganizationUnitId)
                           || (!liveBefore && CompanyEventRules.IsLive(entity));
        if (clashChecked) await RefuseClashAsync(entity, cancellationToken);

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
        return new EditOutcome(moving, moved, postponedNow, changed, clashChecked);
    }

    /// <summary>Tells one event's guests what an edit did (lane 2e-1, 2e-2) — those <paramref name="filter"/> lets through.</summary>
    private async Task<CompanyEventNoticeResultDto?> TellEditAsync(
        CompanyEvent entity, EditOutcome outcome, CancellationToken cancellationToken, Func<EventParticipant, bool>? filter = null)
    {
        if (outcome.Moving)
            return await NotifyRescheduledAsync(entity, cancellationToken, filter);
        if (outcome.PostponedNow)
            // The user's ruling (2e-3): postponed has no date, so the calendar entry is taken away; the move to a
            // new date sends it again.
            return await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventPostponed, null,
                "event postponed", CompanyScheduleNotices.Postponed, filter,
                calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken);
        if (outcome.Changed is { } what)
            return await NotifyParticipantsAsync(entity, CompanyScheduleEmailCatalog.Events.EventChanged,
                tokens =>
                {
                    tokens["WhatChanged"] = what.What;
                    tokens["SiteName"] = what.SiteName;
                    return tokens;
                },
                "event changed", CompanyScheduleNotices.Changed, filter,
                inAppData: new Dictionary<string, object> { ["What"] = what.What },
                // A postponed event's entries were taken away; a new venue must not put them back at the old date.
                calendar: entity.Status == EventStatus.Postponed ? null : HrCalendarMethod.Request,
                cancellationToken: cancellationToken);
        return null;
    }

    public async Task<CompanyEventDto> UpdateAsync(UpdateCompanyEventDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedEventAsync(updateDto.Id, cancellationToken);
        // Lane 2f-2b (D-12): this and following dates, or every date — each still to come.
        if (updateDto.SeriesScope != SeriesScope.ThisOccurrence && entity.RecurrenceSeriesId is not null)
            return await UpdateSeriesAsync(entity, updateDto, tenantId, cancellationToken);

        var outcome = await ApplyEditAsync(entity, updateDto, tenantId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var moving = outcome.Moving;
        var moved = outcome.Moved;

        _logger.LogInformation("Company event updated: {EventNumber}{Moved}", entity.EventNumber, moving ? " (rescheduled)" : string.Empty);

        // Lane 2e-2: who the edit's notice reached, for the save's answer.
        var told = await TellEditAsync(entity, outcome, cancellationToken);
        // D-10: an approved event that moved is approved afresh — its approval was for the old time.
        if (moved?.ApprovalCleared == true) await StartApprovalAsync(entity, cancellationToken);

        // ⚠ Re-read (F-46): the entity's navigations were loaded before the change, so a new organiser,
        // site or unit would answer with the old name.
        var updated = await GetByIdAsync(entity.Id, cancellationToken);
        updated.Told = told;
        if (await AudienceWarningAsync(entity, cancellationToken) is { } warning) updated.Warnings.Add(warning);
        // Lane 2g-2 (C-15): an overlap the server allows is said.
        if (outcome.ClashChecked) updated.Warnings.AddRange(await ClashWarningsAsync(entity, false, cancellationToken));
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
        // Lane 2f-1: a series is approved once (the user's ruling) — not through an occurrence that shares an approval
        // under way on another, and the decision covers every occurrence sharing it.
        await EnsureNotSharedElsewhereAsync(entity, "approve", cancellationToken);
        var sharing = await SharingApprovalAsync(entity, cancellationToken);

        var outcome = await DecideAsync(entity, "Approve", comments);
        // ⚠ The approver's EMPLOYEE id: ApprovedById is an Employee foreign key.
        var adapter = _workflowAdapters.GetAdapter(WorkflowEntityType);
        adapter.ApplyApprovalOutcome(entity, outcome, approvedById);
        // Only a final approval covers the rest: a definition with another stage to go leaves them all waiting.
        var covered = outcome == WorkflowOutcome.Approved && entity.ApprovalDate != null ? sharing : [];
        foreach (var occurrence in covered) adapter.ApplyApprovalOutcome(occurrence, outcome, approvedById);

        await _eventRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company event approval for {EventNumber}: {Outcome}{Series}", entity.EventNumber, outcome,
            covered.Count > 0 ? $", with {covered.Count} more occurrence(s) of its series" : string.Empty);

        // Lane 2e-1: approved at last — the invitations that waited go (F-33), and the organiser is told.
        if (outcome == WorkflowOutcome.Approved && entity.ApprovalDate != null)
        {
            // Lane 2f-2a: across a series, ONE invitation per guest for every date the approval covers (the user's
            // ruling) — it was one per date.
            var invited = covered.Count > 0
                ? await InviteWaitingAcrossAsync([entity, .. covered], cancellationToken)
                : await InviteWaitingGuestsAsync(entity, cancellationToken);
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
        // Lane 2f-1: as for approval — a series is decided once, and not approving it cancels every occurrence sharing it.
        await EnsureNotSharedElsewhereAsync(entity, "reject", cancellationToken);
        var sharing = await SharingApprovalAsync(entity, cancellationToken);

        var outcome = await DecideAsync(entity, "Reject", reason.Trim());
        var adapter = _workflowAdapters.GetAdapter(WorkflowEntityType);
        adapter.ApplyApprovalOutcome(entity, outcome, rejectedById, reason.Trim());

        var change = new CompanyEventChangeDto();
        var covered = outcome == WorkflowOutcome.Rejected ? sharing : [];
        if (outcome == WorkflowOutcome.Rejected)
        {
            change.BookingsCancelled = await CancelLinkedBookingsAsync(
                entity, $"{entity.EventNumber} was not approved: {reason.Trim()}", cancellationToken);
            // Lane 2e-3 (D-14): a guest who held an entry (invited before a move sent it back for approval) loses it.
            entity.CalendarSequence++;
            foreach (var occurrence in covered)
            {
                adapter.ApplyApprovalOutcome(occurrence, outcome, rejectedById, reason.Trim());
                change.BookingsCancelled.AddRange(await CancelLinkedBookingsAsync(
                    occurrence, $"{occurrence.EventNumber} was not approved: {reason.Trim()}", cancellationToken));
                occurrence.CalendarSequence++;
            }
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
            // The occurrences it covered: only a guest invited before a move sent one back for approval was ever told
            // of them, so in the ordinary case this tells nobody.
            foreach (var occurrence in covered)
                change.Told.Add(await NotifyParticipantsAsync(occurrence, CompanyScheduleEmailCatalog.Events.EventCancelled,
                    tokens => { tokens["CancellationReason"] = occurrence.CancellationReason; return tokens; },
                    "event not approved", CompanyScheduleNotices.Cancelled,
                    calendar: HrCalendarMethod.Cancel, cancellationToken: cancellationToken));
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
        // Lane 2f-2b (D-12): this and following dates — the series ends there — or every date still to come.
        if (cancelDto.SeriesScope != SeriesScope.ThisOccurrence && entity.RecurrenceSeriesId is not null)
            return await CancelSeriesAsync(entity, cancelDto, cancellationToken);

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

        // D-10: an approval still under way is withdrawn — there is nothing left to approve. Lane 2f-2b (finding 2): when
        // it was the series' approval, it passes to the next date still waiting.
        if (await CancelApprovalAsync(entity, $"The event was cancelled: {reason}") && entity.RecurrenceSeriesId is { } seriesId)
            await PassSeriesApprovalOnAsync(entity.TenantId, seriesId, cancellationToken);

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
        // Lane 2f-2b (D-12): this and following dates, or every date still to come — each by the same number of days.
        if (rescheduleDto.SeriesScope != SeriesScope.ThisOccurrence && entity.RecurrenceSeriesId is not null)
            return await RescheduleSeriesAsync(entity, rescheduleDto, cancellationToken);

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
        // Lane 2g-2 (C-15): its new time must not clash where the server refuses it — checked before its rooms move.
        await RefuseClashAsync(entity, cancellationToken);

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
        // Lane 2g-2 (C-15): an overlap the server allows is said.
        change.Warnings = await ClashWarningsAsync(entity, false, cancellationToken);
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
        // Lane 2f-2b (finding 2): when it carried the series' approval, the approval passes to the next date waiting.
        if (await CancelApprovalAsync(entity, "The event was deleted.") && entity.RecurrenceSeriesId is { } seriesId)
            await PassSeriesApprovalOnAsync(entity.TenantId, seriesId, cancellationToken);

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
    /// <param name="refuseTwice">False for a series add (lane 2f-2a): a date the guest is already on is passed over
    /// there, not refused.</param>
    private async Task CheckGuestAsync(
        CompanyEvent e, EventParticipant guest, bool isNew, CancellationToken cancellationToken, bool refuseTwice = true)
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
            if (refuseTwice && await others.AnyAsync(p => p.EmployeeId == employeeId, cancellationToken))
                throw new InvalidOperationException($"{person.Name} is already invited to {e.EventName}.");
            return;
        }

        if (!refuseTwice) return;
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
        // Lane 2f-2a (D-12): this and following dates, or every date — each still to come.
        if (createDto.Scope != SeriesScope.ThisOccurrence && ev.RecurrenceSeriesId is not null)
            return await AddSeriesGuestAsync(ev, createDto, tenantId, cancellationToken);
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
    /// <para>F-11: only accepted, declined or tentative is an answer, and only from a guest of the event in the
    /// route. A cancelled or completed event's invitations are closed.</para>
    ///
    /// <para>Lane 2f-2a (D-12): on a series, the same answer for this guest's invitations to this and following dates,
    /// or every date — each still to come; a date they are not invited to is passed over.</para>
    /// </remarks>
    public async Task<EventSeriesGuestResultDto> RespondToInvitationAsync(Guid eventId, RespondToEventInvitationDto responseDto, CancellationToken cancellationToken = default)
    {
        if (!CompanyEventRules.IsAnswer(responseDto.Response))
            throw new InvalidOperationException("Record the answer as accepted, declined or tentative.");

        var tenantId = GetTenantId();
        var guest = await TenantGuests(tenantId)
                .FirstOrDefaultAsync(p => p.Id == responseDto.ParticipantId && p.EventId == eventId, cancellationToken)
            ?? throw new ArgumentException("That guest is not on this event's list.");
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        var comments = CompanyEventRules.Clean(responseDto.ResponseComments);

        if (responseDto.Scope != SeriesScope.ThisOccurrence && ev.RecurrenceSeriesId is not null)
        {
            var (targets, closed) = await SeriesTargetsAsync(ev, responseDto.Scope, cancellationToken);
            var ids = targets.Select(t => t.Id).ToList();
            var rows = await TenantGuests(tenantId)
                .Where(p => ids.Contains(p.EventId))
                .Where(SamePerson(guest.EmployeeId, guest.ExternalParticipantEmail))
                .ToListAsync(cancellationToken);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    $"{GuestName(guest)} is not invited to any date of {ev.EventName} {ScopeWords(responseDto.Scope)} still to come.");

            var answeredAt = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.InvitationStatus = responseDto.Response;
                row.ResponseDate = answeredAt;
                row.ResponseComments = comments;
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var byId = targets.ToDictionary(t => t.Id);
            _logger.LogInformation("{Guest} answered {Answer} for {Count} date(s) of {EventName}",
                GuestName(guest), responseDto.Response, rows.Count, ev.EventName);
            return new EventSeriesGuestResultDto
            {
                EventNumbers = rows.Select(r => byId[r.EventId]).OrderBy(e => e.StartDate).ThenBy(e => e.OccurrenceNumber)
                    .Select(e => e.EventNumber).ToList(),
                Skipped = targets.Count - rows.Count,
                Closed = closed,
            };
        }

        if (CompanyEventRules.IsClosed(ev))
            throw new InvalidOperationException(
                $"{ev.EventName} is {ClosedState(ev)}, so its invitations can no longer be answered.");

        guest.InvitationStatus = responseDto.Response;
        guest.ResponseDate = DateTime.UtcNow;
        guest.ResponseComments = comments;

        await _participantRepository.UpdateAsync(guest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{Guest} answered {Answer} for {EventNumber}", GuestName(guest), responseDto.Response, ev.EventNumber);
        return new EventSeriesGuestResultDto { EventNumbers = [ev.EventNumber] };
    }

    /// <remarks>
    /// <para>Organiser work, on Write (lane 2d) — it needed Admin. Not from a cancelled or completed event, whose
    /// guest list is its record.</para>
    ///
    /// <para>Lane 2f-2a (D-12): on a series, this guest off this and following dates, or every date — each still to come
    /// — and told ONCE, listing the dates, with each date's calendar entry cancelled (the user's ruling).</para>
    /// </remarks>
    public async Task<EventSeriesGuestResultDto> RemoveParticipantAsync(
        Guid participantId, SeriesScope scope = SeriesScope.ThisOccurrence, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var guest = await TenantGuests(tenantId).FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken)
            ?? throw new ArgumentException("That guest was not found.");
        var ev = await GetOwnedEventAsync(guest.EventId, cancellationToken);
        if (scope != SeriesScope.ThisOccurrence && ev.RecurrenceSeriesId is not null)
            return await RemoveSeriesGuestAsync(ev, guest, scope, tenantId, cancellationToken);
        if (CompanyEventRules.IsClosed(ev))
            throw new InvalidOperationException($"{ev.EventName} is {ClosedState(ev)}; its guest list is part of its record.");

        // Lane 2e-3 (D-14): a guest who held an entry is sent its cancellation, which must outrank the entry they hold.
        if (guest.InvitationStatus != InvitationStatus.NotSent) ev.CalendarSequence++;

        await _participantRepository.DeleteAsync(guest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Guest {Guest} removed from {EventNumber}", GuestName(guest), ev.EventNumber);

        // Lane 2e-1: a guest who was invited hears they no longer are (one still waiting for approval never heard).
        if (guest.InvitationStatus != InvitationStatus.NotSent) await TellRemovedGuestAsync(ev, guest, cancellationToken);
        return new EventSeriesGuestResultDto { EventNumbers = [ev.EventNumber] };
    }

    /// <summary>
    /// Takes a guest off several dates of a series at once (lane 2f-2a, D-12): every date the scope reaches that is still
    /// to come and that they are on. Told ONCE (the user's ruling): the single-date email when only one date had invited
    /// them, the series email otherwise — a date still waiting for approval never invited them, so it is not mentioned.
    /// </summary>
    private async Task<EventSeriesGuestResultDto> RemoveSeriesGuestAsync(
        CompanyEvent ev, EventParticipant guest, SeriesScope scope, Guid tenantId, CancellationToken cancellationToken)
    {
        var (targets, closed) = await SeriesTargetsAsync(ev, scope, cancellationToken);
        var byId = targets.ToDictionary(t => t.Id);
        var ids = byId.Keys.ToList();
        var rows = (await TenantGuests(tenantId)
                .Where(p => ids.Contains(p.EventId))
                .Where(SamePerson(guest.EmployeeId, guest.ExternalParticipantEmail))
                .ToListAsync(cancellationToken))
            .Select(p => (Ev: byId[p.EventId], Guest: p))
            .OrderBy(r => r.Ev.StartDate).ThenBy(r => r.Ev.OccurrenceNumber)
            .ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"{GuestName(guest)} is not invited to any date of {ev.EventName} {ScopeWords(scope)} still to come.");

        // Lane 2e-3 (D-14): each date that invited them sends its cancellation, which must outrank the entry they hold.
        var invited = rows.Where(r => r.Guest.InvitationStatus != InvitationStatus.NotSent).ToList();
        foreach (var (date, _) in invited) date.CalendarSequence++;
        foreach (var (_, row) in rows) await _participantRepository.DeleteAsync(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Guest {Guest} removed from {Count} date(s) of {EventName} ({Scope})",
            GuestName(guest), rows.Count, ev.EventName, scope);

        var result = new EventSeriesGuestResultDto
        {
            EventNumbers = rows.Select(r => r.Ev.EventNumber).ToList(),
            Skipped = targets.Count - rows.Count,
            Closed = closed,
        };
        if (invited.Count == 1)
            await TellRemovedGuestAsync(invited[0].Ev, invited[0].Guest, cancellationToken);
        else if (invited.Count > 1)
            result.Told = await TellSeriesChangeAsync(invited, HrCalendarMethod.Cancel, "No longer invited",
                $"You have been taken off the guest list for these dates of {ev.EventName}.", reason: null, nothingRequired: true,
                cancellationToken);
        return result;
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

    /// <inheritdoc />
    /// <remarks>
    /// <para>⚠ Lane 2h (C-18, F-54): the attachment WAS a file name and a path the caller typed, and no file was ever
    /// stored — every row on UAT was one. It is now a file through the upload gate: scanned, stored, and downloadable.</para>
    ///
    /// <para>Not on a cancelled event; a completed one may still take its minutes. The controller resolves the event
    /// before a byte is stored; this check stands against a race.</para>
    /// </remarks>
    public async Task<EventAttachmentDto> AddUploadedAttachmentAsync(
        Guid eventId, EventAttachmentType type, string? description, Guid uploadedById,
        string fileName, string filePath, long fileSize, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ev = await GetOwnedEventAsync(eventId, cancellationToken);
        Refuse(CompanyEventRules.RefuseAttaching(ev.EventName, ev.IsCancelled, ev.Status, type));

        var entity = new EventAttachment
        {
            TenantId = tenantId,
            EventId = eventId,
            FileName = fileName,
            FilePath = filePath,
            Type = type,
            Description = CompanyEventRules.Clean(description),
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
            FileSizeBytes = fileSize,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
        };
        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("File {FileName} attached to event {EventNumber}", fileName, ev.EventNumber);
        return entity.ToDto();
    }

    /// <inheritdoc />
    public async Task<EventAttachmentDto> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        return entity?.ToDto() ?? throw new ArgumentException("That attachment was not found.");
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

    // ── Lane 2g-1: the register's search and export (D-9; C-25) ──

    private const int MaxExportRows = 10_000;

    /// <summary>
    /// The bookings a search finds — this tenant's, untracked, no includes. Text is matched in the number, the room, the
    /// purpose and the booker's name; dates by overlap.
    /// </summary>
    private IQueryable<RoomBooking> BookingSearchQuery(Guid tenantId, RoomBookingSearchDto search)
    {
        var query = _bookingRepository.GetQueryable().AsNoTracking().Where(b => b.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var term = search.Text.Trim();
            query = query.Where(b => b.BookingNumber.Contains(term) || b.Room.RoomName.Contains(term) || b.Purpose.Contains(term)
                                     || (b.BookedBy.FirstName + " " + b.BookedBy.LastName).Contains(term));
        }
        if (search.Status is { } status) query = query.Where(b => b.Status == status);
        if (search.RoomId is { } roomId) query = query.Where(b => b.RoomId == roomId);
        if (search.From is { } from)
        {
            var first = from.Date;
            query = query.Where(b => b.EndDateTime > first);
        }
        if (search.To is { } to)
        {
            var dayAfter = to.Date.AddDays(1);
            query = query.Where(b => b.StartDateTime < dayAfter);
        }
        return query;
    }

    private static IOrderedQueryable<RoomBooking> SortBookings(IQueryable<RoomBooking> query, RoomBookingSearchDto search) =>
        (search.Sort ?? "-start").Trim().ToLowerInvariant() switch
        {
            "start" => query.OrderBy(b => b.StartDateTime).ThenBy(b => b.BookingNumber),
            "number" => query.OrderBy(b => b.BookingNumber),
            "-number" => query.OrderByDescending(b => b.BookingNumber),
            _ => query.OrderByDescending(b => b.StartDateTime).ThenByDescending(b => b.BookingNumber),
        };

    /// <inheritdoc />
    /// <remarks>The page's ids first on narrow rows, then those bookings with their names — as the events' search.</remarks>
    public async Task<PagedResult<RoomBookingDto>> SearchAsync(RoomBookingSearchDto search, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var page = Math.Max(1, search.Page);
        var size = Math.Clamp(search.PageSize, 1, 200);
        var query = BookingSearchQuery(tenantId, search);

        var total = await query.CountAsync(cancellationToken);
        var ids = await SortBookings(query, search).Select(b => b.Id)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(cancellationToken);
        var rows = ids.Count == 0
            ? new List<RoomBooking>()
            : await _bookingRepository.GetQueryable().AsNoTracking()
                .Include(b => b.Room)
                .Include(b => b.BookedBy)
                .Include(b => b.Event)
                .Where(b => b.TenantId == tenantId && ids.Contains(b.Id))
                .ToListAsync(cancellationToken);
        var position = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        return new PagedResult<RoomBookingDto>
        {
            Items = rows.OrderBy(b => position[b.Id]).ToDtoList(),
            TotalCount = total,
            Page = page,
            PageSize = size,
        };
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportCsvAsync(RoomBookingSearchDto search, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rows = await SortBookings(BookingSearchQuery(tenantId, search), search)
            .Select(b => new
            {
                b.BookingNumber,
                b.Room.RoomName,
                b.Purpose,
                Event = b.Event != null ? b.Event.EventName : null,
                BookedBy = b.BookedBy.FirstName + " " + b.BookedBy.LastName,
                b.StartDateTime,
                b.EndDateTime,
                b.ExpectedAttendees,
                b.Status,
                b.ApprovalDate,
                b.CancellationReason,
            })
            .Take(MaxExportRows)
            .ToListAsync(cancellationToken);

        return CompanyScheduleCsv.Build(
            new[] { "Number", "Room", "Purpose", "Event", "Booked by", "Starts", "Ends", "Attendees", "Status", "Approved", "Cancelled because" },
            rows.Select(r => new[]
            {
                r.BookingNumber, r.RoomName, r.Purpose, r.Event, r.BookedBy.Trim(),
                r.StartDateTime.ToString("yyyy-MM-dd HH:mm"), r.EndDateTime.ToString("yyyy-MM-dd HH:mm"),
                r.ExpectedAttendees.ToString(), System.Text.RegularExpressions.Regex.Replace(r.Status.ToString(), "([a-z])([A-Z])", "$1 $2"),
                r.ApprovalDate?.ToString("yyyy-MM-dd"), r.CancellationReason,
            }));
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
