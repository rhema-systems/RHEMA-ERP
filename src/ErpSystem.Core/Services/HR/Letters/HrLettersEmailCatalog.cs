using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Letters;

/// <summary>
/// The HR letters an employee can request: stable event keys, shipped default wording, and the
/// tokens each exposes. Mirrors <c>AssetsEmailCatalog</c> and is consumed the same way — by the
/// template seeder, by <c>TemplatedEmailService</c> as the runtime fallback when a tenant has
/// saved no template, and by the token-catalogue API that drives the authoring palette.
/// </summary>
/// <remarks>
/// <para><b>Every event ships a default body</b>, which is the difference between a feature that
/// works on a fresh tenant and one that waits on a seed nobody remembers to run. HR can rewrite
/// any of them in the template editor without a deployment — and they will, because the exact
/// wording of a letter that leaves the building is precisely what an employer wants in their own
/// voice.</para>
///
/// <para><b>What stays in code is which facts are available; what the letter SAYS belongs to
/// whoever signs it.</b> That is why the conditional clauses below are template
/// <c>{{#if}}</c> blocks rather than C# — the same call the assets catalog documents for its
/// liability wording.</para>
/// </remarks>
public static class HrLettersEmailCatalog
{
    public const string Module = "HrLetters";

    public static class Events
    {
        public const string EmploymentConfirmation = "HrLetterEmploymentConfirmation";
        public const string IntroductionLetter = "HrLetterIntroduction";
        public const string ServiceCertificate = "HrLetterServiceCertificate";
        public const string SalaryConfirmation = "HrLetterSalaryConfirmation";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;

    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    public static EmailEventDescriptor? Find(string eventKey) =>
        All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The letterhead every letter opens with and the signature block it closes on. Kept in one
    /// place so a tenant editing one letter's body does not silently end up with four different
    /// letterheads.
    /// </summary>
    private const string Head =
        "\n<html><body style='font-family:Georgia,serif;color:#1f2937;max-width:720px;margin:0 auto;padding:2rem'>\n" +
        "{{#if CompanyLogoUrl}}<img src='{{CompanyLogoUrl}}' alt='' style='height:56px;margin-bottom:1rem' />{{/if}}\n" +
        "<div style='font-size:0.9rem;line-height:1.4'><strong>{{CompanyName}}</strong>" +
        "{{#if CompanyAddress}}<br/>{{CompanyAddress}}{{/if}}</div>\n" +
        "<hr style='border:none;border-top:2px solid #111827;margin:1rem 0 1.5rem' />\n" +
        "<p style='text-align:right'>{{LetterDate}}</p>\n" +
        "{{#if LetterNumber}}<p style='font-size:0.85rem;color:#6b7280'>Our ref: {{LetterNumber}}</p>{{/if}}\n" +
        "<p>{{AddressedTo}}</p>\n";

    // ⚠ The signatory NAME is guarded, not just the title. `CompanyProfileProvider` reads the
    // name from configuration with no fallback while defaulting the title to "Head of Human
    // Resources", so on a tenant that has not set one the unguarded version rendered an empty
    // bold line above a title — which on a document that leaves the building reads as broken
    // rather than as unsigned. With no signatory configured the letter closes on the company
    // name alone, which is honest.
    private const string Foot =
        "<p style='margin-top:2rem'>Yours faithfully,</p>\n" +
        "<p style='margin-top:2.5rem'>{{#if SignatoryName}}<strong>{{SignatoryName}}</strong>" +
        "{{#if SignatoryTitle}}<br/>{{SignatoryTitle}}{{/if}}<br/>{{/if}}{{CompanyName}}</p>\n" +
        "{{#if CompanyFooter}}<hr style='border:none;border-top:1px solid #e5e7eb;margin:2rem 0 0.75rem' />" +
        "<p style='font-size:0.75rem;color:#6b7280'>{{CompanyFooter}}</p>{{/if}}\n" +
        "</body></html>\n";

    private static string Letter(string title, string body) =>
        Head +
        $"<h2 style='font-size:1.05rem;text-transform:uppercase;letter-spacing:0.04em'>{title}</h2>\n" +
        body +
        Foot;

    /// <summary>The tokens every letter shares. Each event adds its own on top.</summary>
    private static List<EmailTokenDescriptor> CommonTokens() =>
    [
        new("CompanyName", "The employing company's legal name, from the company profile.", "Tema Development Company Ltd"),
        new("CompanyAddress", "The company's registered address.", "P.O. Box 46, Community 1, Tema"),
        new("CompanyLogoUrl", "The company logo, when one is configured.", ""),
        new("CompanyFooter", "The footer line printed on company documents.", "Tema Development Company Limited"),
        new("SignatoryName", "Who signs company letters, from the company profile.", "Ama Serwaa"),
        new("SignatoryTitle", "Their title.", "Head of Human Resources"),
        new("LetterDate", "The date the letter is issued.", "26 August 2026"),
        new("LetterNumber", "The reference printed on the letter.", "HRL-2026-00042"),
        new("AddressedTo", "Who the letter is addressed to.", "To whom it may concern"),
        new("EmployeeName", "The employee's full name.", "Kwabena Owusu"),
        new("EmployeeNumber", "Their staff number.", "TDC/2019/0412"),
        new("PositionTitle", "Their current position.", "Senior Estate Officer"),
        new("DepartmentName", "Their department.", "Estate Management"),
        new("EmploymentType", "Permanent, contract, and so on.", "Permanent"),
        new("DateEmployed", "The date they joined.", "3 June 2019"),
        new("YearsOfService", "Completed years of service.", "7"),
        new("Purpose", "What the employee said they need the letter for.", "a mortgage application"),
    ];

    private static IReadOnlyList<EmailEventDescriptor> Build() =>
    [
        new()
        {
            Module = Module,
            EventKey = Events.EmploymentConfirmation,
            Name = "Letter — employment confirmation",
            Category = "HR Letters",
            Description = "Confirms that a named employee works here, since when, and in what role.",
            DefaultSubject = "Confirmation of employment — {{EmployeeName}}",
            DefaultHtmlBody = Letter("Confirmation of Employment",
                "<p>This is to confirm that <strong>{{EmployeeName}}</strong>, staff number " +
                "<strong>{{EmployeeNumber}}</strong>, is employed by {{CompanyName}}.</p>\n" +
                "<table style='border-collapse:collapse;margin:1rem 0;font-size:0.95rem'>\n" +
                "<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Position</td><td>{{PositionTitle}}</td></tr>\n" +
                "{{#if DepartmentName}}<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Department</td><td>{{DepartmentName}}</td></tr>{{/if}}\n" +
                "<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Employment type</td><td>{{EmploymentType}}</td></tr>\n" +
                "{{#if DateEmployed}}<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Employed since</td><td>{{DateEmployed}}</td></tr>{{/if}}\n" +
                "</table>\n" +
                "<p>This letter is issued at the employee's request{{#if Purpose}} for the purpose of " +
                "{{Purpose}}{{/if}}, and carries no financial obligation on the part of the company.</p>\n"),
            Tokens = CommonTokens(),
        },
        new()
        {
            Module = Module,
            EventKey = Events.IntroductionLetter,
            Name = "Letter — introduction",
            Category = "HR Letters",
            Description = "Introduces an employee to a named third party.",
            DefaultSubject = "Letter of introduction — {{EmployeeName}}",
            DefaultHtmlBody = Letter("Letter of Introduction",
                "<p>The bearer of this letter, <strong>{{EmployeeName}}</strong> (staff number " +
                "{{EmployeeNumber}}), is an employee of {{CompanyName}}, currently serving as " +
                "{{PositionTitle}}{{#if DepartmentName}} in our {{DepartmentName}} department{{/if}}.</p>\n" +
                "{{#if Purpose}}<p>They are introduced to you in connection with {{Purpose}}.</p>{{/if}}\n" +
                "<p>Any courtesy extended to them in the course of their duties will be appreciated. " +
                "Please contact the undersigned should you wish to verify this letter.</p>\n"),
            Tokens = CommonTokens(),
        },
        new()
        {
            Module = Module,
            EventKey = Events.ServiceCertificate,
            Name = "Letter — certificate of service",
            Category = "HR Letters",
            Description = "States the period and capacity in which a person served.",
            DefaultSubject = "Certificate of service — {{EmployeeName}}",
            DefaultHtmlBody = Letter("Certificate of Service",
                "<p>This is to certify that <strong>{{EmployeeName}}</strong>, staff number " +
                "<strong>{{EmployeeNumber}}</strong>, has served {{CompanyName}} " +
                "{{#if DateEmployed}}from {{DateEmployed}}{{/if}} as {{PositionTitle}}" +
                "{{#if DepartmentName}} in the {{DepartmentName}} department{{/if}}.</p>\n" +
                "{{#if YearsOfService}}<p>Completed service to date: {{YearsOfService}} year(s).</p>{{/if}}\n" +
                "<p>This certificate is issued at their request and without prejudice.</p>\n"),
            Tokens = CommonTokens(),
        },
        new()
        {
            Module = Module,
            EventKey = Events.SalaryConfirmation,
            Name = "Letter — employment and salary confirmation",
            Category = "HR Letters",
            Description =
                "Confirms employment and states the salary. Issued only by HR — the employee's own "
                + "profile never displays salary.",
            DefaultSubject = "Confirmation of employment and salary — {{EmployeeName}}",
            DefaultHtmlBody = Letter("Confirmation of Employment and Salary",
                "<p>This is to confirm that <strong>{{EmployeeName}}</strong>, staff number " +
                "<strong>{{EmployeeNumber}}</strong>, is employed by {{CompanyName}} as " +
                "{{PositionTitle}}{{#if DateEmployed}}, and has been since {{DateEmployed}}{{/if}}.</p>\n" +
                "{{#if AnnualSalary}}<p>Their current gross annual salary is " +
                "<strong>{{AnnualSalary}}</strong>.</p>{{/if}}\n" +
                "{{#if Purpose}}<p>This letter is issued at the employee's request for the purpose of " +
                "{{Purpose}}.</p>{{/if}}\n" +
                "<p>The figure stated is gross and before statutory deductions. This letter does not " +
                "constitute a guarantee of continued employment or of the employee's obligations to " +
                "any third party.</p>\n"),
            Tokens =
            [
                .. CommonTokens(),
                new EmailTokenDescriptor(
                    "AnnualSalary",
                    "The gross salary recorded on the employee record, with the tenant's currency. "
                    + "Empty when no salary is recorded — the template then omits the clause.",
                    "GHS 96,000.00"),
            ],
        },
    ];
}

/// <summary>Registers the HR letter templates with the shared templated-email service.</summary>
public sealed class HrLettersEmailEventCatalog : IEmailEventCatalog
{
    public string Module => HrLettersEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => HrLettersEmailCatalog.All;
}
