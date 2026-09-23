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
/// <para>Lifecycle events (enrolled, session changed, completed, certificate issued, plan and task
/// assigned) join this catalogue in lane K-b.</para>
/// </remarks>
public static class OnboardingOrientationEmailCatalog
{
    public const string Module = "OnboardingOrientation";

    public static class Events
    {
        /// <summary>The reminder sweep's per-person digest.</summary>
        public const string ReminderDigest = "OrientationReminderDigest";
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
    };
}

/// <summary>DI-registered wrapper exposing the static <see cref="OnboardingOrientationEmailCatalog"/>.</summary>
public sealed class OnboardingOrientationEmailEventCatalog : IEmailEventCatalog
{
    public string Module => OnboardingOrientationEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => OnboardingOrientationEmailCatalog.All;
}
