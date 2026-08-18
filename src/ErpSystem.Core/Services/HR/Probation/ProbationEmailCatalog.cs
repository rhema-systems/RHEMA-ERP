using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Probation;

/// <summary>
/// The probation module's transactional documents: stable event keys, shipped default
/// subject/body, and the tokens each exposes. Mirrors <c>RecruitmentEmailCatalog</c>, and is
/// consumed the same way — by the template seeder, by <c>TemplatedEmailService</c> as the runtime
/// fallback when a tenant has saved no template, and by the token-catalogue API that drives the
/// authoring palette.
/// </summary>
/// <remarks>
/// FR-HR-032 requires that confirmation "generate a confirmation letter". Because the catalog
/// carries a built-in default, the letter renders on a tenant that has never opened the template
/// editor — which is the difference between a requirement that works out of the box and one that
/// waits on a seed nobody remembers to run.
/// </remarks>
public static class ProbationEmailCatalog
{
    public const string Module = "Probation";

    public static class Events
    {
        /// <summary>FR-HR-032 — issued once the probation has been confirmed.</summary>
        public const string ConfirmationLetter = "ProbationConfirmationLetter";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;

    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    public static EmailEventDescriptor? Find(string eventKey) =>
        All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    private const string LetterShell =
        "\n<html><body style='font-family:Georgia,serif;color:#1f2937;max-width:720px;margin:0 auto;padding:2rem'>\n" +
        "{{#if CompanyLogoUrl}}<img src='{{CompanyLogoUrl}}' alt='' style='height:56px;margin-bottom:1rem' />{{/if}}\n" +
        "<div style='font-size:0.9rem;line-height:1.4'><strong>{{CompanyName}}</strong>" +
        "{{#if CompanyAddress}}<br/>{{CompanyAddress}}{{/if}}</div>\n" +
        "<hr style='border:none;border-top:2px solid #111827;margin:1rem 0 1.5rem' />\n" +
        "<p style='text-align:right'>{{LetterDate}}</p>\n" +
        "<p>{{EmployeeName}}<br/>{{EmployeeNumber}}</p>\n" +
        "<h2 style='font-size:1.05rem;text-transform:uppercase;letter-spacing:0.04em'>Confirmation of Appointment</h2>\n" +
        "<p>Dear {{EmployeeName}},</p>\n" +
        "<p>Following the successful completion of your probationary period as " +
        "<strong>{{PositionTitle}}</strong>{{#if DepartmentName}} in {{DepartmentName}}{{/if}}, " +
        "I am pleased to confirm your appointment with effect from <strong>{{ConfirmationDate}}</strong>.</p>\n" +
        "<p>Your probation ran from {{ProbationStartDate}} to {{ProbationEndDate}} " +
        "({{DurationMonths}} months){{#if ExtensionText}}, {{ExtensionText}}{{/if}}.</p>\n" +
        "{{#if ReviewSummary}}<p>{{ReviewSummary}}</p>{{/if}}\n" +
        "<p>All other terms and conditions of your employment remain unchanged. " +
        "On behalf of {{CompanyName}}, congratulations, and thank you for your contribution so far.</p>\n" +
        "<p style='margin-top:2rem'>Yours sincerely,</p>\n" +
        "<p style='margin-top:2.5rem'><strong>{{SignatoryName}}</strong><br/>{{SignatoryTitle}}</p>\n" +
        "{{#if CompanyFooter}}<hr style='border:none;border-top:1px solid #e5e7eb;margin-top:2rem' />" +
        "<p style='font-size:0.75rem;color:#6b7280'>{{CompanyFooter}}</p>{{/if}}\n" +
        "</body></html>";

    private static IReadOnlyList<EmailEventDescriptor> Build() => new List<EmailEventDescriptor>
    {
        new()
        {
            Module = Module,
            EventKey = Events.ConfirmationLetter,
            Name = "Probation Confirmation Letter",
            Category = "Probation & Confirmation",
            Description = "Issued by HR once a probation has been confirmed (FR-HR-032). "
                        + "Renders as a self-contained HTML document suitable for print-to-PDF.",
            DefaultSubject = "Confirmation of Appointment — {{EmployeeName}}",
            DefaultHtmlBody = LetterShell,
            Tokens = new List<EmailTokenDescriptor>
            {
                new() { Token = "CompanyName",        Description = "Legal employer name",              SampleValue = "Tema Development Company Ltd" },
                new() { Token = "CompanyAddress",     Description = "Registered address",               SampleValue = "P. O. Box 46, Tema" },
                new() { Token = "CompanyLogoUrl",     Description = "Letterhead logo",                  SampleValue = "" },
                new() { Token = "CompanyFooter",      Description = "Document footer text",             SampleValue = "" },
                new() { Token = "LetterDate",         Description = "Date the letter is issued",        SampleValue = "18 August 2026" },
                new() { Token = "EmployeeName",       Description = "Employee being confirmed",         SampleValue = "Ama Mensah" },
                new() { Token = "EmployeeNumber",     Description = "Employee number",                  SampleValue = "TDC/0417" },
                new() { Token = "PositionTitle",      Description = "Position held",                    SampleValue = "Estate Officer" },
                new() { Token = "DepartmentName",     Description = "Department or unit",               SampleValue = "Estates" },
                new() { Token = "StaffLevel",         Description = "Staff category",                   SampleValue = "Senior Staff" },
                new() { Token = "ProbationStartDate", Description = "Probation start",                  SampleValue = "1 January 2026" },
                new() { Token = "ProbationEndDate",   Description = "Probation end actually reached",   SampleValue = "1 July 2026" },
                new() { Token = "DurationMonths",     Description = "Probation length in months",       SampleValue = "6" },
                new() { Token = "ExtensionText",      Description = "Present only when extended",       SampleValue = "extended once" },
                new() { Token = "ConfirmationDate",   Description = "Date the appointment is confirmed", SampleValue = "1 July 2026" },
                new() { Token = "ReviewSummary",      Description = "Outcome of the final review",      SampleValue = "Your final probation review recorded a recommendation to confirm." },
                new() { Token = "SignatoryName",      Description = "Signing officer",                  SampleValue = "Head of Human Resources" },
                new() { Token = "SignatoryTitle",     Description = "Signing officer's title",          SampleValue = "Head of Human Resources" },
            },
        },
    };
}

/// <summary>DI-registered wrapper exposing the static <see cref="ProbationEmailCatalog"/>.</summary>
public sealed class ProbationEmailEventCatalog : IEmailEventCatalog
{
    public string Module => ProbationEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => ProbationEmailCatalog.All;
}
