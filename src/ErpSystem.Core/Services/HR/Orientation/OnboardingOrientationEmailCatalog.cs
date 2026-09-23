using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Orientation;

/// <summary>
/// Orientation & onboarding emails (round 4, lane K): stable event keys, shipped default subject and
/// body, and the tokens each exposes. Consumed like every other catalogue — by
/// <c>TemplatedEmailService</c> as the runtime fallback when a tenant has saved no template, by the
/// template seeder, and by the token catalogue that drives the authoring palette.
/// </summary>
/// <remarks>
/// <para><b>One digest, not one email per item.</b> The reminder sweep sends each person ONE message
/// per run listing everything due to them — an onboarding coordinator with forty open tasks gets one
/// email, not forty. The list arrives as plain text in <c>ItemList</c>, one line per item, and the
/// template keeps its line breaks (<c>white-space: pre-line</c>): the renderer HTML-encodes tokens,
/// so a task or employee name can never become markup.</para>
///
/// <para><b>Lifecycle notices (lane K-b)</b> — enrolled, a session placed, moved, put off or called
/// off, completed, certificate issued. One email per person per event, queued with its in-app
/// notice and sent by the dispatcher. Their shipped default is one plain layout driven by
/// <c>Headline</c> and <c>Body</c>, composed in code so the in-app notice and the email say the same
/// thing; each event also exposes its own facts as tokens, for a tenant who rewrites a template.</para>
/// </remarks>
public static class OnboardingOrientationEmailCatalog
{
    public const string Module = "OnboardingOrientation";

    public static class Events
    {
        /// <summary>The reminder sweep's per-person digest.</summary>
        public const string ReminderDigest = "OrientationReminderDigest";

        public const string Enrolled = "OrientationEnrolled";
        public const string SessionScheduled = "OrientationSessionScheduled";
        public const string SessionRescheduled = "OrientationSessionRescheduled";
        public const string SessionPostponed = "OrientationSessionPostponed";
        public const string SessionCancelled = "OrientationSessionCancelled";
        public const string Completed = "OrientationCompleted";
        public const string CertificateIssued = "OrientationCertificateIssued";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;

    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    private const string DigestBody =
        "\n<html><body style='font-family:Segoe UI,Arial,sans-serif;color:#1f2937;max-width:640px;margin:0 auto;padding:1.5rem'>\n" +
        "<p style='font-size:0.85rem;color:#6b7280;margin:0 0 1rem'><strong>{{CompanyName}}</strong> · Orientation and onboarding</p>\n" +
        "<p>Dear {{RecipientName}},</p>\n" +
        "<p>{{Summary}}</p>\n" +
        "<div style='white-space:pre-line;background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;padding:0.75rem 1rem;line-height:1.6'>{{ItemList}}</div>\n" +
        "{{#if ActionUrl}}<p style='margin-top:1.25rem'><a href='{{ActionUrl}}' style='background:#111827;color:#ffffff;padding:0.55rem 1rem;border-radius:6px;text-decoration:none;display:inline-block'>{{ActionLabel}}</a></p>{{/if}}\n" +
        "<p style='font-size:0.75rem;color:#6b7280;margin-top:2rem'>Each item is reminded once for each date it falls due, " +
        "and again if it becomes overdue. Nothing further is needed from you for anything already done.</p>\n" +
        "</body></html>";

    private static IReadOnlyList<EmailEventDescriptor> Build() => new List<EmailEventDescriptor>
    {
        new()
        {
            Module = Module,
            EventKey = Events.ReminderDigest,
            Name = "Orientation & Onboarding Reminder",
            Category = "Orientation & Onboarding",
            Description = "Sent by the daily reminder sweep: one message per person listing their onboarding tasks "
                        + "and orientations that are due soon, overdue or waiting on them.",
            DefaultSubject = "{{Headline}}",
            DefaultHtmlBody = DigestBody,
            Tokens = new List<EmailTokenDescriptor>
            {
                new("RecipientName", "The person the reminder is for", "Akpene Amoah"),
                new("Headline", "One line: the single item, or how many there are", "3 onboarding and orientation items need your attention"),
                new("Summary", "A sentence introducing the list", "These onboarding tasks and orientations are due soon or overdue:"),
                new("ItemList", "One line per item — what, whose, and when", "• Laptop and accounts — Kofi Mensah — due tomorrow\n• Anti-Harassment & Code of Conduct — due in 5 days"),
                new("ItemCount", "How many items the message lists", "3"),
                new("ActionUrl", "Where the button goes", "http://localhost:3000/me/orientation"),
                new("ActionLabel", "The button's text", "Open my orientations"),
                new("CompanyName", "Legal employer name", "Tema Development Company Ltd"),
            },
        },

        Notice(Events.Enrolled, "Orientation Enrolment",
            "Sent when somebody is enrolled in a programme — by HR, by an audience rule, or when a recurring "
            + "programme's next cycle opens. Not sent for a programme whose \"Send reminders\" is off.",
            new("ProgrammeTitle", "The programme", "Anti-Harassment & Code of Conduct"),
            new("DueDate", "When it must be completed by, if it has a deadline", "Friday, 17 October 2026"),
            new("SessionTitle", "The session they were placed on, if any", "October induction"),
            new("SessionWhen", "When the session runs", "Tuesday, 14 October 2026, 09:00–12:00"),
            new("SessionWhere", "Where, or the link for an online session", "Board Room, Head Office"),
            new("WaitlistPosition", "Their place on the waiting list, when the session was full", "3"),
            new("Reason", "Why they were enrolled", "All staff, every year")),

        Notice(Events.SessionScheduled, "Orientation Session Scheduled",
            "Sent to a participant placed on a session, and to an employee facilitating one when it goes live.",
            new("ProgrammeTitle", "The programme", "New Staff Induction"),
            new("SessionTitle", "The session", "October induction"),
            new("SessionWhen", "When it runs", "Tuesday, 14 October 2026, 09:00–12:00"),
            new("SessionWhere", "Where, or the link", "Board Room, Head Office"),
            new("Role", "\"participant\" or \"facilitator\"", "participant"),
            new("Instructions", "What participants were asked to bring or do", "Bring your staff ID.")),

        Notice(Events.SessionRescheduled, "Orientation Session Moved",
            "Sent to everyone on a live session — participants and employee facilitators — when its date, "
            + "time, place or link changes.",
            new("ProgrammeTitle", "The programme", "New Staff Induction"),
            new("SessionTitle", "The session", "October induction"),
            new("Role", "\"participant\" or \"facilitator\"", "participant"),
            new("WasWhen", "When it was", "Tuesday, 14 October 2026, 09:00–12:00"),
            new("WasWhere", "Where it was", "Board Room, Head Office"),
            new("SessionWhen", "When it is now", "Thursday, 16 October 2026, 09:00–12:00"),
            new("SessionWhere", "Where it is now", "Training Room 2")),

        Notice(Events.SessionPostponed, "Orientation Session Postponed",
            "Sent to everyone on a live session when it is postponed with no new date yet.",
            new("ProgrammeTitle", "The programme", "New Staff Induction"),
            new("SessionTitle", "The session", "October induction"),
            new("Role", "\"participant\" or \"facilitator\"", "participant"),
            new("WasWhen", "When it was to run", "Tuesday, 14 October 2026, 09:00–12:00")),

        Notice(Events.SessionCancelled, "Orientation Session Cancelled",
            "Sent to everyone on a live session when it is cancelled. A participant's enrolment in the programme stands.",
            new("ProgrammeTitle", "The programme", "New Staff Induction"),
            new("SessionTitle", "The session", "October induction"),
            new("Role", "\"participant\" or \"facilitator\"", "participant"),
            new("SessionWhen", "When it was to run", "Tuesday, 14 October 2026, 09:00–12:00")),

        Notice(Events.Completed, "Orientation Completed",
            "Sent the first time somebody completes a programme — with their certificate when the programme issues one.",
            new("ProgrammeTitle", "The programme", "Anti-Harassment & Code of Conduct"),
            new("CompletedOn", "The day it was completed", "Wednesday, 1 October 2026"),
            new("CertificateNumber", "The certificate issued with it, if the programme issues one", "OCERT-2026-00012"),
            new("CertificateExpires", "When that certificate expires, if it does", "Friday, 1 October 2027"),
            new("NextCycleOpens", "For a recurring programme, when the next cycle opens", "Tuesday, 15 September 2027")),

        Notice(Events.CertificateIssued, "Orientation Certificate Issued",
            "Sent when HR issues or reissues a certificate by hand. A certificate issued at completion "
            + "travels with the completion notice instead.",
            new("ProgrammeTitle", "The programme", "Anti-Harassment & Code of Conduct"),
            new("CertificateNumber", "The certificate's serial", "OCERT-2026-00012"),
            new("CertificateIssuedOn", "The day it was issued", "Wednesday, 1 October 2026"),
            new("CertificateExpires", "When it expires, if it does", "Friday, 1 October 2027")),
    };

    private const string NoticeBody =
        "\n<html><body style='font-family:Segoe UI,Arial,sans-serif;color:#1f2937;max-width:640px;margin:0 auto;padding:1.5rem'>\n" +
        "<p style='font-size:0.85rem;color:#6b7280;margin:0 0 1rem'><strong>{{CompanyName}}</strong> · Orientation and onboarding</p>\n" +
        "<p>Dear {{RecipientName}},</p>\n" +
        "<p style='font-weight:600'>{{Headline}}</p>\n" +
        "<div style='white-space:pre-line;line-height:1.6'>{{Body}}</div>\n" +
        "{{#if ActionUrl}}<p style='margin-top:1.25rem'><a href='{{ActionUrl}}' style='background:#111827;color:#ffffff;padding:0.55rem 1rem;border-radius:6px;text-decoration:none;display:inline-block'>{{ActionLabel}}</a></p>{{/if}}\n" +
        "</body></html>";

    private static EmailEventDescriptor Notice(
        string eventKey, string name, string description, params EmailTokenDescriptor[] specific) => new()
    {
        Module = Module,
        EventKey = eventKey,
        Name = name,
        Category = "Orientation & Onboarding",
        Description = description,
        DefaultSubject = "{{Headline}}",
        DefaultHtmlBody = NoticeBody,
        Tokens = new List<EmailTokenDescriptor>
        {
            new("RecipientName", "The person the notice is for", "Akpene Amoah"),
            new("Headline", "One line: what happened", "You have been enrolled in Anti-Harassment & Code of Conduct"),
            new("Body", "The notice itself, as plain lines — the same words as the in-app notice", "Complete it by Friday, 17 October 2026."),
            new("ActionUrl", "Where the button goes, when there is somewhere to go", "http://localhost:3000/me/orientation"),
            new("ActionLabel", "The button's text", "Open the orientation"),
            new("CompanyName", "Legal employer name", "Tema Development Company Ltd"),
        }.Concat(specific).ToList(),
    };
}

/// <summary>DI-registered wrapper exposing the static <see cref="OnboardingOrientationEmailCatalog"/>.</summary>
public sealed class OnboardingOrientationEmailEventCatalog : IEmailEventCatalog
{
    public string Module => OnboardingOrientationEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => OnboardingOrientationEmailCatalog.All;
}
