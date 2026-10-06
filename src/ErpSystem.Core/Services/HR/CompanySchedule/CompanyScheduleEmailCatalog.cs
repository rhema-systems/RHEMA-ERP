using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// The company-schedule module's transactional emails: stable event keys, shipped default
/// subject/body, and the tokens each exposes.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane D6.</b> The module could already invite people to a meeting, take their
/// RSVP, reschedule and cancel — and told nobody about any of it. <c>EventParticipant</c> carries
/// <c>InvitationSentDate</c>, which was written by the participant-create and meant nothing, because
/// no invitation was ever sent. An event with an RSVP deadline and no invitation is a deadline
/// nobody has heard of.</para>
///
/// <para>Consumed the same way as every other HR catalogue: by the template seeder, by
/// <c>TemplatedEmailService</c> as the runtime fallback when a tenant has saved no template, and by
/// the token-catalogue API that drives the authoring palette. Because the catalogue carries a
/// built-in default, these render on a tenant that has never opened the template editor.</para>
///
/// <para>⚠ Registering this in <c>HrModuleServiceRegistration</c> is not optional. Without the
/// <c>IEmailEventCatalog</c> registration <c>TemplatedEmailService</c> has no fallback for the
/// module and every send throws instead of rendering its shipped default — the trap lane F recorded
/// for the interview paper.</para>
/// </remarks>
public static class CompanyScheduleEmailCatalog
{
    public const string Module = "CompanySchedule";

    public static class Events
    {
        /// <summary>Sent when somebody is added to an event's participant list.</summary>
        public const string EventInvitation = "EventInvitation";

        /// <summary>Sent to participants who have not answered as the RSVP deadline approaches.</summary>
        public const string EventRsvpReminder = "EventRsvpReminder";

        /// <summary>Sent the configured number of days before the event itself.</summary>
        public const string EventReminder = "EventReminder";

        /// <summary>Sent to every participant when the event moves.</summary>
        public const string EventRescheduled = "EventRescheduled";

        /// <summary>Sent to every participant when the event is called off.</summary>
        public const string EventCancelled = "EventCancelled";

        /// <summary>Sent to the organiser when the event is approved (lane 2e-1).</summary>
        public const string EventApproved = "EventApproved";

        /// <summary>Sent to every guest who was invited when the event is postponed (lane 2e-1).</summary>
        public const string EventPostponed = "EventPostponed";

        /// <summary>Sent to every guest who was invited when the venue, site or joining link changes (lane 2e-1).</summary>
        public const string EventChanged = "EventChanged";

        /// <summary>Sent to a guest who was invited when they are taken off the guest list (lane 2e-1).</summary>
        public const string EventGuestRemoved = "EventGuestRemoved";

        /// <summary>Sent to an employee when a task on an event is given to them (lane 2e-1, F-34).</summary>
        public const string EventTaskAssigned = "EventTaskAssigned";

        /// <summary>Sent once, by the hourly sweep, to the assignee of a task past its due date (lane 2e-3, F-34).</summary>
        public const string EventTaskOverdue = "EventTaskOverdue";

        /// <summary>
        /// Sent once to a guest invited to several dates of a recurring event at once, listing them, each with its
        /// calendar entry (lane 2f-2a, D-12: the user's ruling, one notice per guest per series action).
        /// </summary>
        public const string EventSeriesInvitation = "EventSeriesInvitation";

        /// <summary>
        /// Sent once to a guest when several dates of a recurring event change together for them — taken off the guest
        /// list (lane 2f-2a); moved, changed or cancelled (lane 2f-2b) — listing the dates.
        /// </summary>
        public const string EventSeriesChanged = "EventSeriesChanged";

        /// <summary>
        /// Sent to the booker when a booking that needed approval is approved, at its last stage (lane 3b-1, F-34: the
        /// user's ruling, every outcome told).
        /// </summary>
        public const string BookingApproved = "BookingApproved";

        /// <summary>
        /// Sent to the booker when a booking is cancelled any way — by the desk, with its event, by retiring its room — or
        /// not approved, with the reason (lane 3b-1, F-34). Never for their own act.
        /// </summary>
        public const string BookingCancelled = "BookingCancelled";

        /// <summary>Sent to the booker when the desk marks a booking a no-show — booked and not used (lane 3b-2).</summary>
        public const string BookingNoShow = "BookingNoShow";

        /// <summary>
        /// Sent once to a booker when one act approves, does not approve or cancels several of their bookings together,
        /// listing them (lane 3d-1, the user's ruling: told once per act, as a series' guests are).
        /// </summary>
        public const string BookingsChanged = "BookingsChanged";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;

    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    public static EmailEventDescriptor? Find(string eventKey) =>
        All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    // ── Shell ──────────────────────────────────────────────────────────────────
    private const string BlueGradient = "linear-gradient(135deg,#1e3a8a,#1a56db)";
    private const string AmberGradient = "linear-gradient(135deg,#92400e,#b45309)";
    private const string GreyGradient = "linear-gradient(135deg,#374151,#6b7280)";

    private static string Shell(string gradient, string headerHtml, string innerHtml) =>
        "\n<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>\n" +
        $"<div style='background:{gradient};padding:2rem;border-radius:8px 8px 0 0'>\n" +
        $"  <h1 style='color:#fff;margin:0;font-size:1.5rem'>{headerHtml}</h1>\n" +
        "</div>\n" +
        "<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>\n" +
        innerHtml + "\n" +
        "</div>\n</body></html>";

    /// <summary>
    /// The when-and-where block every one of these needs.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>{{#if EventTime}}</c> guards the time, because an ALL-DAY event has none and printing
    /// "09:00–17:00" for one would be inventing a precision the record does not carry — the same
    /// distinction the clash check draws between precise and day-granular commitments.
    /// </remarks>
    private const string WhenAndWhere = @"
  <div style='background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:1rem;margin:1rem 0'>
    <div style='font-size:1.125rem;font-weight:700;color:#111827'>{{EventName}}</div>
    <div style='color:#6b7280;font-size:0.875rem;margin-top:0.25rem'>{{EventNumber}}</div>
    <div style='margin-top:0.75rem'><strong>When:</strong> {{EventDate}}{{#if EventTime}}, {{EventTime}}{{/if}}</div>
    {{#if VenueName}}<div style='margin-top:0.25rem'><strong>Where:</strong> {{VenueName}}</div>{{/if}}
    {{#if OnlineMeetingLink}}<div style='margin-top:0.25rem'><strong>Join:</strong> <a href='{{OnlineMeetingLink}}'>{{OnlineMeetingLink}}</a></div>{{/if}}
    {{#if OrganizerName}}<div style='margin-top:0.25rem;color:#6b7280;font-size:0.875rem'>Organised by {{OrganizerName}}</div>{{/if}}
  </div>";

    private static EmailTokenDescriptor T(string token, string desc, string sample) => new(token, desc, sample);

    /// <summary>The tokens every event email carries, so a template author sees the same names everywhere.</summary>
    private static List<EmailTokenDescriptor> CommonTokens() => new()
    {
        T("ParticipantName", "Who the email is addressed to.", "Ama Serwaa"),
        T("EventName", "The event's title.", "Q4 Management Review"),
        T("EventNumber", "The event's reference.", "EVT-2026-00042"),
        T("EventDate", "The date it falls on.", "Tuesday, 14 October 2026"),
        T("EventTime", "The time window; ABSENT on an all-day event, and the line is hidden with it.", "09:00 – 11:00"),
        T("VenueName", "Where it is held; hidden when the event is online-only.", "Boardroom, Head Office"),
        T("OnlineMeetingLink", "The joining link; hidden when the event is in person.", "https://meet.example.com/q4"),
        T("OrganizerName", "Who called the meeting.", "Kwabena Mensah"),
        T("CompanyName", "The employer's name, from the company profile.", "Tema Development Corporation"),
    };

    private static List<EmailEventDescriptor> Build()
    {
        var list = new List<EmailEventDescriptor>();

        // ── 1. Invitation ──────────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventInvitation,
            Name = "Event Invitation",
            Category = "Invitation",
            Description =
                "Sent when somebody is added to an event's participant list. Until round 4 the module "
                + "recorded an InvitationSentDate and sent nothing, so an RSVP deadline was a deadline "
                + "the invitee had never heard of.",
            DefaultSubject = "{{EventName}} — {{EventDate}}",
            DefaultHtmlBody = Shell(BlueGradient, "You are invited",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>You have been invited to the following{{#if IsRequired}} and your attendance is required{{/if}}.</p>" +
                WhenAndWhere + @"
  {{#if Description}}<p>{{Description}}</p>{{/if}}
  {{#if RsvpDeadline}}<p style='background:#fef3c7;border-left:3px solid #d97706;padding:0.75rem'>
    Please confirm whether you can attend by <strong>{{RsvpDeadline}}</strong>.
  </p>{{/if}}
  {{#if SpecialRequirements}}<p style='color:#6b7280;font-size:0.875rem'>{{SpecialRequirements}}</p>{{/if}}"),
            Tokens = CommonTokens().Concat(new[]
            {
                T("IsRequired", "Truthy when attendance is required rather than optional.", "true"),
                T("Description", "The event's description; the paragraph is hidden when empty.", "Quarterly review of departmental performance."),
                T("RsvpDeadline", "When a response is needed by; the whole block is hidden when the event asks for none.", "Friday, 10 October 2026"),
                T("SpecialRequirements", "Anything recorded against this participant; hidden when empty.", "Please bring the draft budget."),
            }).ToList(),
        });

        // ── 2. RSVP chase ──────────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventRsvpReminder,
            Name = "RSVP Reminder",
            Category = "Invitation",
            Description =
                "Sent to participants who have not yet answered, as the RSVP deadline approaches. Goes "
                + "only to those whose invitation is unanswered — never to somebody who has already "
                + "accepted or declined.",
            DefaultSubject = "Still need your answer: {{EventName}}",
            DefaultHtmlBody = Shell(AmberGradient, "We still need your answer",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>We have not yet had your response to this invitation.</p>" +
                WhenAndWhere + @"
  {{#if RsvpDeadline}}<p>Please let us know by <strong>{{RsvpDeadline}}</strong>.</p>{{/if}}"),
            Tokens = CommonTokens().Concat(new[]
            {
                T("RsvpDeadline", "When a response is needed by; hidden when the event asks for none.", "Friday, 10 October 2026"),
            }).ToList(),
        });

        // ── 3. Reminder ────────────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventReminder,
            Name = "Event Reminder",
            Category = "Reminder",
            Description =
                "Sent the configured number of days before the event. The event carries SendReminders "
                + "and ReminderDaysBefore; both were settable and read by nothing.",
            DefaultSubject = "Reminder: {{EventName}} on {{EventDate}}",
            DefaultHtmlBody = Shell(BlueGradient, "Coming up",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>A reminder that this is coming up{{#if DaysUntil}} in {{DaysUntil}} day(s){{/if}}.</p>" +
                WhenAndWhere),
            Tokens = CommonTokens().Concat(new[]
            {
                T("DaysUntil", "How many days away it is; the clause is hidden when it is today.", "2"),
            }).ToList(),
        });

        // ── 4. Rescheduled ─────────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventRescheduled,
            Name = "Event Rescheduled",
            Category = "Change",
            Description =
                "Sent to every participant when the event moves. Carries what it moved FROM as well as "
                + "to — which is only possible since D7 fixed C-2, where the original window was "
                + "overwritten and lost.",
            DefaultSubject = "Moved: {{EventName}} is now {{EventDate}}",
            DefaultHtmlBody = Shell(AmberGradient, "This has been moved",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p><strong>{{EventName}}</strong> has been rescheduled.</p>
  {{#if OriginalWhen}}<p style='color:#6b7280'>
    Previously: <s>{{OriginalWhen}}</s>
  </p>{{/if}}" +
                WhenAndWhere + @"
  {{#if RescheduleReason}}<p><strong>Why:</strong> {{RescheduleReason}}</p>{{/if}}
  <p style='color:#6b7280;font-size:0.875rem'>
    If you had already told us whether you could attend, please confirm again for the new time.
  </p>"),
            Tokens = CommonTokens().Concat(new[]
            {
                T("OriginalWhen", "What it was moved FROM; hidden where the original was not recorded — which is every event moved before round 4.", "Tuesday, 7 October 2026, 09:00 – 11:00"),
                T("RescheduleReason", "Why it moved; hidden when none was given.", "The Managing Director is travelling."),
            }).ToList(),
        });

        // ── 5. Cancelled ───────────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventCancelled,
            Name = "Event Cancelled",
            Category = "Change",
            Description = "Sent to every participant when the event is called off.",
            DefaultSubject = "Cancelled: {{EventName}} on {{EventDate}}",
            DefaultHtmlBody = Shell(GreyGradient, "This has been cancelled",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p><strong>{{EventName}}</strong>, which was to be held on {{EventDate}}, has been cancelled.</p>
  {{#if CancellationReason}}<p><strong>Why:</strong> {{CancellationReason}}</p>{{/if}}
  <p style='color:#6b7280;font-size:0.875rem'>Nothing is required of you; please remove it from your diary.</p>"),
            Tokens = CommonTokens().Concat(new[]
            {
                T("CancellationReason", "Why it was called off; hidden when none was given.", "Postponed pending the audit."),
            }).ToList(),
        });

        // ── 6. Approved (lane 2e-1) ────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventApproved,
            Name = "Event Approved",
            Category = "Approval",
            Description =
                "Sent to the organiser when an event that needs approval is approved — after its last approval "
                + "stage. Its waiting invitations go out at the same moment: an event awaiting approval invites "
                + "nobody (lane 2e-1, F-33). Until then the organiser was told nothing either way.",
            DefaultSubject = "Approved: {{EventName}} on {{EventDate}}",
            DefaultHtmlBody = Shell(BlueGradient, "Approved",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p><strong>{{EventName}}</strong>, which you organise, has been approved{{#if ApprovedBy}} by {{ApprovedBy}}{{/if}}.</p>" +
                WhenAndWhere + @"
  {{#if InvitationsSent}}<p>The {{InvitationsSent}} invitation(s) that were waiting for the approval have now gone out.</p>{{/if}}"),
            Tokens = CommonTokens().Concat(new[]
            {
                T("ApprovedBy", "Who gave the final approval; hidden when it is not known.", "Efua Asante"),
                T("InvitationsSent", "How many waiting invitations went out with the approval; hidden when none.", "6"),
            }).ToList(),
        });

        // ── 7. Postponed (lane 2e-1) ───────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventPostponed,
            Name = "Event Postponed",
            Category = "Change",
            Description =
                "Sent to every guest who was invited when the event is postponed: it has no new date yet. A guest "
                + "still waiting for the event's approval was never invited and is not told.",
            DefaultSubject = "Postponed: {{EventName}}",
            DefaultHtmlBody = Shell(AmberGradient, "This has been postponed",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p><strong>{{EventName}}</strong>, which was to be held on {{EventDate}}, has been postponed.</p>
  <p style='color:#6b7280;font-size:0.875rem'>A new date will follow. Nothing is required of you until then.</p>"),
            Tokens = CommonTokens(),
        });

        // ── 8. Changed (lane 2e-1) ─────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventChanged,
            Name = "Event Details Changed",
            Category = "Change",
            Description =
                "Sent to every guest who was invited when the event's venue, site or joining link changes and its "
                + "time does not (a new time is Event Rescheduled). Until lane 2e-1 a new venue reached nobody.",
            DefaultSubject = "Changed: {{EventName}} on {{EventDate}}",
            DefaultHtmlBody = Shell(AmberGradient, "Details have changed",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>{{WhatChanged}} for <strong>{{EventName}}</strong> has changed. The details now:</p>" +
                WhenAndWhere + @"
  {{#if SiteName}}<p><strong>Site:</strong> {{SiteName}}</p>{{/if}}"),
            Tokens = CommonTokens().Concat(new[]
            {
                T("WhatChanged", "What changed: the venue, the joining link, or both.", "The venue"),
                T("SiteName", "The site the event is now at, when the site changed; hidden otherwise.", "Head Office"),
            }).ToList(),
        });

        // ── 9. Taken off the guest list (lane 2e-1) ────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventGuestRemoved,
            Name = "Event Guest Removed",
            Category = "Invitation",
            Description =
                "Sent to a guest who was invited when they are taken off the guest list. Until lane 2e-1 an "
                + "uninvited guest was never told, and still expected.",
            DefaultSubject = "No longer needed: {{EventName}} on {{EventDate}}",
            DefaultHtmlBody = Shell(GreyGradient, "Off the guest list",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>You have been taken off the guest list for <strong>{{EventName}}</strong> on {{EventDate}}.</p>
  <p style='color:#6b7280;font-size:0.875rem'>Nothing is required of you; please remove it from your diary.</p>"),
            Tokens = CommonTokens(),
        });

        // ── 10. A task (lane 2e-1, F-34) ───────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventTaskAssigned,
            Name = "Event Task Assigned",
            Category = "Task",
            Description =
                "Sent to an employee when a task on an event is given to them — when it is added, or passed to "
                + "them. Until lane 2e-1 an assignee learnt of a task only by opening the event.",
            DefaultSubject = "A task for {{EventName}}",
            DefaultHtmlBody = Shell(BlueGradient, "A task for you",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>You have been given a task for <strong>{{EventName}}</strong>:</p>
  <p style='background:#fff;border-left:3px solid #1a56db;padding:0.75rem'>{{TaskDescription}}</p>
  {{#if TaskDue}}<p>Due: <strong>{{TaskDue}}</strong>{{#if TaskPriority}} — {{TaskPriority}} priority{{/if}}</p>{{/if}}" +
                WhenAndWhere),
            Tokens = CommonTokens().Concat(new[]
            {
                T("TaskDescription", "What is to be done.", "Book the caterer and confirm numbers."),
                T("TaskDue", "When it is due; the line is hidden when it has no due date.", "Friday, 10 October 2026"),
                T("TaskPriority", "Its priority.", "High"),
            }).ToList(),
        });

        // ── 11. A task overdue (lane 2e-3, F-34) ───────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventTaskOverdue,
            Name = "Event Task Overdue",
            Category = "Task",
            Description =
                "Sent once, by the hourly sweep, to the employee a task is given to, the first time it passes its due "
                + "date unfinished. Sent again only if the due date moves or the task passes to someone else. The "
                + "organiser sees the task marked Overdue on the event page.",
            DefaultSubject = "Overdue: a task for {{EventName}}",
            DefaultHtmlBody = Shell(BlueGradient, "A task is overdue",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>This task for <strong>{{EventName}}</strong> was due on <strong>{{TaskDue}}</strong> and is not yet done:</p>
  <p style='background:#fff;border-left:3px solid #1a56db;padding:0.75rem'>{{TaskDescription}}</p>
  {{#if TaskPriority}}<p>Priority: {{TaskPriority}}</p>{{/if}}
  <p>Please finish it, or tell the organiser{{#if OrganizerName}}, {{OrganizerName}},{{/if}} if it cannot be done.</p>" +
                WhenAndWhere),
            Tokens = CommonTokens().Concat(new[]
            {
                T("TaskDescription", "What is to be done.", "Book the caterer and confirm numbers."),
                T("TaskDue", "The date it was due.", "Friday, 10 October 2026"),
                T("TaskPriority", "Its priority.", "High"),
            }).ToList(),
        });

        // ── 12. Invited to a series (lane 2f-2a, D-12) ─────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventSeriesInvitation,
            Name = "Event Series Invitation",
            Category = "Invitation",
            Description =
                "Sent once to a guest invited to several dates of a recurring event at once — added to the series, "
                + "invited when the series is approved, or carried onto the dates a series is extended by. It lists "
                + "the dates, and each date's calendar entry is attached. A single date is invited with Event "
                + "Invitation.",
            DefaultSubject = "{{EventName}} — {{DateCount}} dates from {{EventDate}}",
            DefaultHtmlBody = Shell(BlueGradient, "You are invited",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>You have been invited to <strong>{{EventName}}</strong>{{#if SeriesPattern}}, {{SeriesPattern}}{{/if}}, on these
  {{DateCount}} dates{{#if IsRequired}}, and your attendance is required{{/if}}:</p>
  {{{SeriesDates}}}
  <div style='background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:1rem;margin:1rem 0'>
    {{#if VenueName}}<div><strong>Where:</strong> {{VenueName}}</div>{{/if}}
    {{#if OnlineMeetingLink}}<div style='margin-top:0.25rem'><strong>Join:</strong> <a href='{{OnlineMeetingLink}}'>{{OnlineMeetingLink}}</a></div>{{/if}}
    {{#if OrganizerName}}<div style='margin-top:0.25rem;color:#6b7280;font-size:0.875rem'>Organised by {{OrganizerName}}</div>{{/if}}
  </div>
  {{#if Description}}<p>{{Description}}</p>{{/if}}
  {{#if RsvpDeadline}}<p style='background:#fef3c7;border-left:3px solid #d97706;padding:0.75rem'>
    Please say for each date whether you can attend. The first answer is needed by <strong>{{RsvpDeadline}}</strong>.
  </p>{{/if}}
  {{#if SpecialRequirements}}<p style='color:#6b7280;font-size:0.875rem'>{{SpecialRequirements}}</p>{{/if}}
  <p style='color:#6b7280;font-size:0.875rem'>Each date's calendar entry is attached.</p>"),
            Tokens = CommonTokens().Concat(new[]
            {
                SeriesDatesToken(),
                T("DateCount", "How many dates the invitation is for.", "4"),
                T("SeriesPattern", "How the event repeats; hidden when it is not known.", "every week"),
                T("IsRequired", "Truthy when attendance is required rather than optional.", "true"),
                T("Description", "The event's description; the paragraph is hidden when empty.", "Weekly operations stand-up."),
                T("RsvpDeadline", "The first date's reply-by date; the block is hidden when the event asks for no answer.", "Friday, 10 October 2026"),
                T("SpecialRequirements", "Anything recorded against this guest; hidden when empty.", "Please bring the weekly figures."),
            }).ToList(),
        });

        // ── 13. A series changed for a guest (lanes 2f-2a, 2f-2b, D-12) ────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.EventSeriesChanged,
            Name = "Event Series Changed",
            Category = "Change",
            Description =
                "Sent once to a guest when several dates of a recurring event change for them together, listing the "
                + "dates: taken off the guest list (lane 2f-2a); and, from lane 2f-2b, moved, changed or cancelled. "
                + "Each date's updated calendar entry, or its cancellation, is attached. A single date's change uses the "
                + "single-event email.",
            DefaultSubject = "{{ChangeTitle}}: {{EventName}} — {{DateCount}} dates",
            DefaultHtmlBody = Shell(AmberGradient, "{{ChangeTitle}}",
                @"  <p>Hi <strong>{{ParticipantName}}</strong>,</p>
  <p>{{ChangeSummary}}</p>
  {{{SeriesDates}}}
  {{#if Reason}}<p><strong>Why:</strong> {{Reason}}</p>{{/if}}
  {{#if NothingRequired}}<p style='color:#6b7280;font-size:0.875rem'>Nothing is required of you; please remove these dates from your diary.</p>{{/if}}
  <p style='color:#6b7280;font-size:0.875rem'>Each date's calendar entry is attached, to update your calendar.</p>"),
            Tokens = CommonTokens().Concat(new[]
            {
                SeriesDatesToken(),
                T("DateCount", "How many dates changed for this guest.", "3"),
                T("ChangeTitle", "The change in a few words: No longer invited, Moved, Changed or Cancelled.", "No longer invited"),
                T("ChangeSummary", "The change in a sentence, naming the event.", "You have been taken off the guest list for these dates of Weekly Operations Stand-up."),
                T("Reason", "Why, when one was given; hidden otherwise.", "The meeting is moving to Tuesdays."),
                T("NothingRequired", "Truthy when the guest need do nothing but clear their diary (taken off the list, cancelled).", "true"),
            }).ToList(),
        });

        // ── 14. A room booking approved (lane 3b-1) ────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.BookingApproved,
            Name = "Room Booking Approved",
            Category = "Booking",
            Description =
                "Sent to whoever booked a meeting room that needs approval, when the booking is approved at its last "
                + "stage (lane 3b-1, F-34). Until then the booking is tentative and holds the room.",
            DefaultSubject = "Approved: {{RoomName}} on {{BookingDate}}",
            DefaultHtmlBody = Shell(BlueGradient, "Booking approved",
                @"  <p>Hi <strong>{{BookerName}}</strong>,</p>
  <p>Your booking of <strong>{{RoomName}}</strong> has been approved{{#if ApprovedBy}} by {{ApprovedBy}}{{/if}}. The room is yours.</p>" +
                BookingBlock),
            Tokens = BookingTokens().Concat(new[]
            {
                T("ApprovedBy", "Who gave the final approval; hidden when it is not known.", "Efua Asante"),
            }).ToList(),
        });

        // ── 15. A room booking cancelled, or not approved (lane 3b-1) ──────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.BookingCancelled,
            Name = "Room Booking Cancelled",
            Category = "Booking",
            Description =
                "Sent to whoever booked a meeting room when the booking is cancelled any way — by the desk, with the "
                + "event it was for, or because the room was taken out of use — or is not approved, with the reason "
                + "(lane 3b-1, F-34). Nobody is told of their own act.",
            DefaultSubject = "{{CancelTitle}}: {{RoomName}} on {{BookingDate}}",
            DefaultHtmlBody = Shell(GreyGradient, "{{CancelTitle}}",
                @"  <p>Hi <strong>{{BookerName}}</strong>,</p>
  <p>Your booking of <strong>{{RoomName}}</strong> {{CancelSentence}}, and the room is released.</p>" +
                BookingBlock + @"
  {{#if CancellationReason}}<p><strong>Why:</strong> {{CancellationReason}}</p>{{/if}}"),
            Tokens = BookingTokens().Concat(new[]
            {
                T("CancelTitle", "The outcome in a few words: Booking cancelled, or Booking not approved.", "Booking cancelled"),
                T("CancelSentence", "The outcome as the middle of a sentence: has been cancelled, or was not approved.", "has been cancelled"),
                T("CancellationReason", "Why; hidden when none was recorded.", "Boardroom was taken out of use."),
            }).ToList(),
        });

        // ── 16. A room booking marked a no-show (lane 3b-2) ────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.BookingNoShow,
            Name = "Room Booking No-Show",
            Category = "Booking",
            Description =
                "Sent to whoever booked a meeting room when the desk marks the booking a no-show: the room was held and not "
                + "used (lane 3b-2). It cannot be undone. Nobody is told of their own act.",
            DefaultSubject = "Marked a no-show: {{RoomName}} on {{BookingDate}}",
            DefaultHtmlBody = Shell(AmberGradient, "Marked a no-show",
                @"  <p>Hi <strong>{{BookerName}}</strong>,</p>
  <p>Your booking of <strong>{{RoomName}}</strong> has been marked a no-show: the room was held for you and not used.</p>" +
                BookingBlock + @"
  <p style='color:#6b7280;font-size:0.875rem'>If the room was used, or you could not cancel in time, please speak to the HR desk.</p>"),
            Tokens = BookingTokens(),
        });

        // ── 17. Several room bookings changed together (lane 3d-1) ─────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.BookingsChanged,
            Name = "Room Bookings Changed",
            Category = "Booking",
            Description =
                "Sent once to whoever booked meeting rooms when one act approves, does not approve or cancels several of "
                + "their bookings together — the dates of a series booked at once, a series cancelled, a room taken out of "
                + "use — listing them, each with its reason (lane 3d-1). One booking uses the single-booking email. Nobody "
                + "is told of their own act.",
            DefaultSubject = "{{ChangeTitle}}: {{BookingCount}} room bookings",
            DefaultHtmlBody = Shell(BlueGradient, "{{ChangeTitle}}",
                @"  <p>Hi <strong>{{BookerName}}</strong>,</p>
  <p>{{ChangeSummary}}</p>
  {{{BookingList}}}"),
            Tokens = new List<EmailTokenDescriptor>
            {
                T("BookerName", "Who booked the rooms, to whom the email is addressed.", "Ama Serwaa"),
                T("ChangeTitle", "The outcome in a few words: Bookings approved, Bookings not approved or Bookings cancelled.", "Bookings approved"),
                T("ChangeSummary", "The outcome in a sentence.", "These bookings of yours have been approved by Efua Asante. The rooms are yours."),
                T("BookingCount", "How many bookings it is about.", "4"),
                BookingListToken(),
                T("CompanyName", "The employer's name, from the company profile.", "Tema Development Corporation"),
            },
        });

        return list;
    }

    /// <summary>A booking's room, day, time and purpose (lane 3b-1).</summary>
    private const string BookingBlock = @"
  <div style='background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:1rem;margin:1rem 0'>
    <div style='font-size:1.125rem;font-weight:700;color:#111827'>{{RoomName}}</div>
    <div style='color:#6b7280;font-size:0.875rem;margin-top:0.25rem'>{{BookingNumber}}</div>
    <div style='margin-top:0.75rem'><strong>When:</strong> {{BookingDate}}, {{BookingTime}}</div>
    {{#if Purpose}}<div style='margin-top:0.25rem'><strong>For:</strong> {{Purpose}}</div>{{/if}}
    {{#if EventName}}<div style='margin-top:0.25rem;color:#6b7280;font-size:0.875rem'>Booked for {{EventName}}</div>{{/if}}
  </div>";

    /// <summary>The tokens every booking email carries (lane 3b-1).</summary>
    private static List<EmailTokenDescriptor> BookingTokens() => new()
    {
        T("BookerName", "Who booked the room, to whom the email is addressed.", "Ama Serwaa"),
        T("BookingNumber", "The booking's reference.", "BK-2026-00042"),
        T("RoomName", "The room booked.", "Boardroom"),
        T("BookingDate", "The day of the booking.", "Tuesday, 14 October 2026"),
        T("BookingTime", "The hours booked.", "09:00 – 11:00"),
        T("Purpose", "What the room is for; hidden when blank.", "Management committee"),
        T("EventName", "The event the booking is for; hidden when it is for none.", "Q4 Management Review"),
        T("CompanyName", "The employer's name, from the company profile.", "Tema Development Corporation"),
    };

    /// <summary>
    /// The list of a series' dates, built by the application (each date and time, and its event number) and emitted
    /// RAW — <c>{{{SeriesDates}}}</c> — so it is declared HTML for the template editor's check.
    /// </summary>
    private static EmailTokenDescriptor SeriesDatesToken() => new(
        "SeriesDates",
        "The dates, as a list the system builds: each date and time, and its event number. Use it raw: {{{SeriesDates}}}.",
        "<ul><li>Monday, 6 October 2026, 09:00 – 10:00 (EVT-2026-00051)</li><li>Monday, 13 October 2026, 09:00 – 10:00 (EVT-2026-00052)</li></ul>")
    {
        IsHtml = true,
    };

    /// <summary>
    /// The bookings a several-at-once email lists (lane 3d-1), built by the application — each room, time and number, and its
    /// reason when there is one — and emitted RAW, <c>{{{BookingList}}}</c>, so it is declared HTML.
    /// </summary>
    private static EmailTokenDescriptor BookingListToken() => new(
        "BookingList",
        "The bookings, as a list the system builds: each room, time and booking number, and why when it was cancelled. Use it raw: {{{BookingList}}}.",
        "<ul><li>Boardroom — Monday 6 October 2026, 09:00–11:00 (BK-2026-00051)</li><li>Boardroom — Monday 13 October 2026, 09:00–11:00 (BK-2026-00052)</li></ul>")
    {
        IsHtml = true,
    };
}

/// <summary>DI-registered wrapper exposing the static <see cref="CompanyScheduleEmailCatalog"/>.</summary>
public sealed class CompanyScheduleEmailEventCatalog : IEmailEventCatalog
{
    public string Module => CompanyScheduleEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => CompanyScheduleEmailCatalog.All;
}
