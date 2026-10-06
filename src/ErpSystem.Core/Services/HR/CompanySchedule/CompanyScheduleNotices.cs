using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// Who hears what about a company event in the app (company-schedule final closure, lane 2e-1): one topic per
/// notice and audience, <c>CompanySchedule.{Notice}.{Guest|Organiser|Assignee}</c> — travel's model
/// (<see cref="StaffTravelNotices"/>), chosen at lane 2's source check.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> The module told people by email alone, and only through its five templated emails: a guest who
/// does not read their mail never heard they were invited, moved or uninvited, and nothing at all told an organiser
/// their event was approved or an assignee they had a task (F-31, F-34).</para>
///
/// <para><b>In the app only.</b> Email stays on the templated catalogue (<see cref="CompanyScheduleEmailCatalog"/>):
/// the topic path queues rows with no delivery result, would lose the catalogue's wording, and cannot carry an
/// attachment (the calendar invite, D-14). So the topics are seeded with email off, and the caller sends the email.</para>
///
/// <para><b>Nineteen topics since lane 3b-2</b> (the overdue task at 2e-3; a series invited, a series changed at 2f-2a; a
/// room booking approved, not approved, cancelled — to its booker — at 3b-1; marked a no-show at 3b-2).
/// A tenant seeded with fewer gets the rest the next time any notice is raised: <see cref="EnsureTopicsAsync"/> adds
/// whatever key is missing.</para>
///
/// <para><b>Who.</b> The employees given, through the active logins linked to them; an employee with no login is told
/// by email alone. <b>Nobody is told of their own act</b> — the signed-in user is left out — except of a reminder or a
/// chase, which are about the date, not the act.</para>
///
/// <para><b>The link.</b> A guest's opens their own schedule at the event's first day, which every employee can open;
/// an organiser's or an assignee's opens the event's page, where their work is.</para>
///
/// <para><b>Never fails the act.</b> Every caller has already saved; a notice that cannot be raised is logged, not
/// thrown, and the publisher saves its rows on the caller's unit of work, so callers raise notices only after their own
/// save. Tenant-explicit: the hourly reminder sweep uses it with nobody signed in.</para>
///
/// <para><b>Delivered (lane 2e-2).</b> <see cref="TellAsync"/> answers whom it reached: a notice in the app counts as
/// delivered, beside an email the mail server took, so an invitation is Sent and a reminder stamped once either reached
/// somebody (the user's ruling, 2026-10-05).</para>
/// </remarks>
public sealed class CompanyScheduleNotices
{
    public const string TopicEntityType = "CompanySchedule";
    public const string ToGuest = "Guest";
    public const string ToOrganiser = "Organiser";
    public const string ToAssignee = "Assignee";
    /// <summary>Lane 3b-1: whoever booked a meeting room.</summary>
    public const string ToBooker = "Booker";
    private const string RecipientsKey = "RecipientUserIds";

    // ---- the notices (the middle of the topic key) ----
    public const string Invited = "Invited";
    public const string RsvpChase = "RsvpChase";
    public const string Reminder = "Reminder";
    public const string Rescheduled = "Rescheduled";
    public const string Postponed = "Postponed";
    public const string Changed = "Changed";
    public const string Cancelled = "Cancelled";
    public const string Removed = "Removed";
    public const string Approved = "Approved";
    public const string NotApproved = "NotApproved";
    public const string TaskAssigned = "TaskAssigned";
    public const string TaskOverdue = "TaskOverdue";
    // Lane 2f-2a (D-12): one notice per guest per series action, raised on the first date it covers.
    public const string SeriesInvited = "SeriesInvited";
    public const string SeriesChanged = "SeriesChanged";
    // Lane 3b-1 (F-34, the user's ruling: every outcome told): a room booking's, to its booker.
    public const string BookingApproved = "BookingApproved";
    public const string BookingNotApproved = "BookingNotApproved";
    public const string BookingCancelled = "BookingCancelled";
    // Lane 3b-2: marked a no-show.
    public const string BookingNoShow = "BookingNoShow";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<CompanyScheduleNotices> _logger;

    // Per scope: one act raises several notices.
    private readonly HashSet<Guid> _topicsEnsured = new();
    private (bool Known, Guid? EmployeeId) _actorEmployee;

    public CompanyScheduleNotices(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        UserManager<ApplicationUser> userManager,
        ICurrentUserProvider currentUser,
        ILogger<CompanyScheduleNotices> logger)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _userManager = userManager;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ---- links ----

    /// <summary>The reader's own schedule, opened at the event's first day — every employee can open it.</summary>
    public static string GuestLink(CompanyEvent e) =>
        $"/hr/company-schedule/my-schedule?from={e.StartDate:yyyy-MM-dd}&event={e.Id}";

    /// <summary>The event's page, where an organiser's or an assignee's work is.</summary>
    public static string EventLink(CompanyEvent e) => $"/hr/company-schedule/events/{e.Id}";

    /// <summary>The signed-in user, or null with nobody signed in (the sweep).</summary>
    private Guid? ActorUserId => _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;

    /// <summary>The employee the signed-in user is linked to, if any — whom an act's emails leave out.</summary>
    public async Task<Guid?> ActorEmployeeIdAsync(CancellationToken cancellationToken = default)
    {
        if (_actorEmployee.Known) return _actorEmployee.EmployeeId;
        Guid? employeeId = null;
        if (ActorUserId is Guid actor)
            employeeId = await _userManager.Users.Where(u => u.Id == actor).Select(u => u.EmployeeId).FirstOrDefaultAsync(cancellationToken);
        _actorEmployee = (true, employeeId);
        return employeeId;
    }

    /// <summary>
    /// Tells the given employees in the app, through their active logins — leaving out the signed-in user unless
    /// <paramref name="actorToo"/> (a reminder or a chase).
    /// </summary>
    /// <param name="data">The notice's own tokens (what changed, a task), added to the event's.</param>
    /// <returns>
    /// The employees it reached — those with an active login it was raised to (lane 2e-2: a notice in the app counts as
    /// delivered). Empty when it could not be raised. The handlers write the rows in this scope and log their own
    /// failures, so this is who it was raised to, not a read-back of the rows.
    /// </returns>
    public Task<IReadOnlySet<Guid>> TellAsync(
        CompanyEvent e, string notice, string audience, IEnumerable<Guid> employeeIds,
        IReadOnlyDictionary<string, object>? data = null, bool actorToo = false,
        CancellationToken cancellationToken = default) =>
        // The event's id is every notice's entity: the suites' teardowns find an event's notices by it.
        RaiseAsync(e.TenantId, e.Id, e.EventNumber, notice, audience, employeeIds, new Dictionary<string, object>
        {
            ["EventName"] = e.EventName,
            ["EventNumber"] = e.EventNumber,
            ["When"] = CompanyEventRules.Describe(EventWindow.Of(e)),
            ["ActionPath"] = audience == ToGuest ? GuestLink(e) : EventLink(e),
        }, data, actorToo, cancellationToken);

    /// <summary>
    /// Tells a room booking's booker of its outcome in the app (lane 3b-1, F-34) — never of their own act. The booking's
    /// id is the notice's entity. The link opens the booking.
    /// </summary>
    public Task<IReadOnlySet<Guid>> TellBookerAsync(
        RoomBooking b, string roomName, string notice, IReadOnlyDictionary<string, object>? data = null,
        CancellationToken cancellationToken = default) =>
        RaiseAsync(b.TenantId, b.Id, b.BookingNumber, notice, ToBooker, [b.BookedById], new Dictionary<string, object>
        {
            ["BookingNumber"] = b.BookingNumber,
            ["RoomName"] = roomName,
            ["When"] = RoomBookingRules.Describe(RoomBookingRules.AsUtc(b.StartDateTime), RoomBookingRules.AsUtc(b.EndDateTime)),
            ["ActionPath"] = BookingLink(b),
        }, data, actorToo: false, cancellationToken);

    /// <summary>
    /// The booking's page in the portal (lane 3c, the user's ruling): every booker notice goes to the booker, and that page
    /// opens for any booker, HR officers included — the HR page answers 403 to staff. The approver's inbox link stays on the
    /// HR page (<c>WorkflowEntityDisplayService</c>).
    /// </summary>
    public static string BookingLink(RoomBooking b) => $"/me/room-bookings/{b.Id}";

    private async Task<IReadOnlySet<Guid>> RaiseAsync(
        Guid tenantId, Guid entityId, string reference, string notice, string audience, IEnumerable<Guid> employeeIds,
        Dictionary<string, object> tokens, IReadOnlyDictionary<string, object>? data, bool actorToo,
        CancellationToken cancellationToken)
    {
        var none = new HashSet<Guid>();
        try
        {
            var ids = employeeIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0) return none;

            var actor = ActorUserId;
            var logins = (await _userManager.Users
                    .Where(u => u.TenantId == tenantId && u.IsActive && u.EmployeeId != null && ids.Contains(u.EmployeeId.Value))
                    .Select(u => new { u.Id, EmployeeId = u.EmployeeId!.Value })
                    .ToListAsync(cancellationToken))
                .Where(u => actorToo || u.Id != actor)
                .ToList();
            var users = logins.Select(u => u.Id).Distinct().ToList();
            if (users.Count == 0) return none;

            await EnsureTopicsAsync(tenantId, cancellationToken);

            tokens[RecipientsKey] = users;
            if (data is not null)
                foreach (var (key, value) in data)
                    tokens[key] = value;

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = notice,
                Audience = audience,
                EntityId = entityId,
                TriggeredByUserId = actor,
                Data = tokens,
            }, cancellationToken);
            return logins.Select(u => u.EmployeeId).ToHashSet();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Company schedule notice {Notice} to the {Audience} of {Reference} could not be raised",
                notice, audience, reference);
            return none;
        }
    }

    // ---- the topics ----

    private sealed record TopicSeed(string Notice, string Audience, string Name, string Description, string Title, string Body);

    /// <remarks>
    /// Admins can change any topic's words on the Notification Topics screen; these are the defaults a tenant gets the
    /// first time a company-schedule notice is raised. The words carry the event's name, number and time — never a
    /// reason or a note: a notice travels further than the record, and the email carries the rest.
    /// </remarks>
    private static readonly TopicSeed[] Seeds =
    {
        new(Invited, ToGuest, "Company events: you are invited (guest)",
            "Sent in the app to an employee guest when they are invited — or, for an event that needs approval, when it is approved. The invitation email goes separately.",
            "You are invited: {{EventName}}",
            "{{EventNumber}} — {{When}}. It is on your schedule."),
        new(RsvpChase, ToGuest, "Company events: your answer is needed (guest)",
            "Sent in the app to an employee guest who has not answered, as the reply-by date approaches, with the RSVP chase email.",
            "Your answer is needed: {{EventName}}",
            "{{EventNumber}} — {{When}}. Please say whether you can attend."),
        new(Reminder, ToGuest, "Company events: coming up (guest)",
            "Sent in the app to an employee guest who has not declined, with the event reminder email.",
            "Coming up: {{EventName}}",
            "{{EventNumber}} — {{When}}."),
        new(Reminder, ToOrganiser, "Company events: coming up (organiser)",
            "Sent in the app to the event's organiser with the event reminder, whether or not they are on the guest list.",
            "Coming up: {{EventName}}, which you organise",
            "{{EventNumber}} — {{When}}."),
        new(Rescheduled, ToGuest, "Company events: moved (guest)",
            "Sent in the app to an employee guest who was invited when the event moves. The email says why and from when.",
            "Moved: {{EventName}}",
            "{{EventNumber}} is now {{When}}. If you had answered, please answer again for the new time."),
        new(Postponed, ToGuest, "Company events: postponed (guest)",
            "Sent in the app to an employee guest who was invited when the event is postponed.",
            "Postponed: {{EventName}}",
            "{{EventNumber}}, {{When}}, is postponed. A new date will follow."),
        new(Changed, ToGuest, "Company events: venue or link changed (guest)",
            "Sent in the app to an employee guest who was invited when the event's venue, site or joining link changes. The email has the new details.",
            "Changed: {{EventName}}",
            "{{What}} for {{EventNumber}} ({{When}}) changed. The email has the new details."),
        new(Cancelled, ToGuest, "Company events: cancelled (guest)",
            "Sent in the app to an employee guest who was invited when the event is cancelled — or not approved.",
            "Cancelled: {{EventName}}",
            "{{EventNumber}}, {{When}}, has been cancelled. Nothing is required of you."),
        new(Removed, ToGuest, "Company events: taken off the guest list (guest)",
            "Sent in the app to an employee guest who was invited when they are taken off the guest list.",
            "You are no longer invited: {{EventName}}",
            "{{EventNumber}}, {{When}}. You have been taken off the guest list; nothing is required of you."),
        new(Approved, ToOrganiser, "Company events: approved (organiser)",
            "Sent in the app to the event's organiser when the event is approved, with the approval email. Its waiting invitations go out at the same time.",
            "Approved: {{EventName}}",
            "{{EventNumber}} — {{When}} — is approved, and its guests are invited."),
        new(NotApproved, ToOrganiser, "Company events: not approved (organiser)",
            "Sent in the app to the event's organiser when the event is not approved, and so cancelled. The email says why.",
            "Not approved: {{EventName}}",
            "{{EventNumber}} — {{When}} — was not approved, and is cancelled. The email says why."),
        new(TaskAssigned, ToAssignee, "Company events: a task for you (assignee)",
            "Sent in the app to an employee when a task on an event is given to them, with the task email.",
            "A task for {{EventName}}",
            "{{Task}}{{Due}}. For {{EventNumber}}, {{When}}."),
        new(TaskOverdue, ToAssignee, "Company events: a task overdue (assignee)",
            "Sent in the app, once, to the employee a task is given to when it passes its due date unfinished (lane 2e-3), with the overdue email. Again only if the due date moves or the task passes to someone new.",
            "Overdue: a task for {{EventName}}",
            "{{Task}} was due {{DueOn}} and is not yet done. For {{EventNumber}}, {{When}}."),
        new(SeriesInvited, ToGuest, "Company events: invited to several dates (guest)",
            "Sent in the app, once, to an employee guest invited to several dates of a recurring event at once (lane 2f-2a), with the series invitation email, which lists the dates.",
            "You are invited: {{EventName}}, {{Count}} dates",
            "From {{When}}. The dates are on your schedule; the email lists them."),
        new(SeriesChanged, ToGuest, "Company events: several dates changed (guest)",
            "Sent in the app, once, to an employee guest when several dates of a recurring event change for them together (lane 2f-2a: taken off the guest list), with the series email, which lists the dates.",
            "{{What}}: {{EventName}}, {{Count}} dates",
            "From {{When}}. The email lists the dates."),
        new(BookingApproved, ToBooker, "Room bookings: approved (booker)",
            "Sent in the app to whoever booked a room that needs approval, when the booking is approved at its last stage (lane 3b-1), with the approval email.",
            "Approved: {{RoomName}}",
            "{{BookingNumber}} — {{When}}. The room is yours."),
        new(BookingNotApproved, ToBooker, "Room bookings: not approved (booker)",
            "Sent in the app to whoever booked a room when the booking is not approved, and so cancelled (lane 3b-1). The email says why.",
            "Not approved: {{RoomName}}",
            "{{BookingNumber}} — {{When}} — was not approved, and the room is released. The email says why."),
        new(BookingCancelled, ToBooker, "Room bookings: cancelled (booker)",
            "Sent in the app to whoever booked a room when the booking is cancelled by somebody else — by the desk, with the event it was for, or because the room was taken out of use (lane 3b-1). The email says why.",
            "Cancelled: {{RoomName}}",
            "{{BookingNumber}} — {{When}} — has been cancelled, and the room is released. The email says why."),
        new(BookingNoShow, ToBooker, "Room bookings: marked a no-show (booker)",
            "Sent in the app to whoever booked a room when the desk marks the booking a no-show — held and not used (lane 3b-2), with the no-show email.",
            "Marked a no-show: {{RoomName}}",
            "{{BookingNumber}} — {{When}} — was marked a no-show: the room was held and not used."),
    };

    /// <summary>Seeds the topics a tenant does not have yet. Every notice calls it; once per tenant per scope.</summary>
    public async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_topicsEnsured.Contains(tenantId)) return;

        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = Seeds.Select(Key).ToArray();
        var existing = new HashSet<string>(await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken), StringComparer.OrdinalIgnoreCase);

        if (existing.Count < Seeds.Length)
        {
            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            foreach (var seed in Seeds)
            {
                var key = Key(seed);
                if (existing.Contains(key)) continue;

                var topic = new NotificationTopic
                {
                    TenantId = tenantId, Key = key, Name = seed.Name, Description = seed.Description,
                    EntityType = TopicEntityType, IsSystem = true, IsActive = true,
                    // ⚠ Email off: the templated catalogue sends it (see the class remarks).
                    EnableInApp = true, EnableEmail = false, EnableSms = false,
                    InAppTitleTemplate = seed.Title,
                    InAppBodyTemplate = seed.Body,
                    ActionUrlTemplate = "{{ActionPath}}",
                    CreatedBy = "System",
                };
                await topicRepo.AddAsync(topic);
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId, TopicId = topic.Id,
                    RecipientKind = "UsersFromData", RecipientValue = RecipientsKey,
                    IsSystem = true, SendInApp = true, SendEmail = false, CreatedBy = "System",
                });
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _topicsEnsured.Add(tenantId);
    }

    private static string Key(TopicSeed seed) => $"{TopicEntityType}.{seed.Notice}.{seed.Audience}";
}
