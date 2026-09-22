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

        return list;
    }
}

/// <summary>DI-registered wrapper exposing the static <see cref="CompanyScheduleEmailCatalog"/>.</summary>
public sealed class CompanyScheduleEmailEventCatalog : IEmailEventCatalog
{
    public string Module => CompanyScheduleEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => CompanyScheduleEmailCatalog.All;
}
