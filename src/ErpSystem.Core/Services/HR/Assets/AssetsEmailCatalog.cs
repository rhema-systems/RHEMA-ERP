using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Assets;

/// <summary>
/// The staff-assets module's transactional documents: stable event keys, shipped default
/// subject/body, and the tokens each exposes. Mirrors <c>ProbationEmailCatalog</c> and
/// <c>RecruitmentEmailCatalog</c>, and is consumed the same way — by the template seeder, by
/// <c>TemplatedEmailService</c> as the runtime fallback when a tenant has saved no template, and by
/// the token-catalogue API that drives the authoring palette.
/// </summary>
/// <remarks>
/// <para>AST-5 asks for a responsibility-and-terms document to print and sign, AST-5b for the same
/// document by email. Because the catalog carries a built-in default, both work on a tenant that has
/// never opened the template editor — the difference between a requirement that works out of the box
/// and one that waits on a seed nobody remembers to run.</para>
///
/// <para><b>⚠ The liability wording is a template conditional, not C#.</b> Whether an employee is
/// answerable for loss and for damage are two flags on the assignment, and the sentence each
/// produces is the part a client is most likely to want in their own words — so
/// <c>{{#if ResponsibleForLoss}} … {{else}} … {{/if}}</c> puts it where HR can edit it. What stays
/// in code is which flag is true; what the flag *says* belongs to whoever signs the form.</para>
///
/// <para>Note the <c>{{else}}</c> branches state non-liability <b>explicitly</b> rather than falling
/// silent. Silence on a signed form reads as the standard clause to the person signing it, and the
/// record — not the form — is what a surcharge (slice 7) and an exit settlement (FR-HR-184) are
/// computed from.</para>
/// </remarks>
public static class AssetsEmailCatalog
{
    public const string Module = "Assets";

    public static class Events
    {
        /// <summary>AST-5 / AST-5b — the form an employee signs for a company asset.</summary>
        public const string ResponsibilityTerms = "AssetResponsibilityTerms";
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
        "<h2 style='font-size:1.05rem;text-transform:uppercase;letter-spacing:0.04em'>" +
        "Company Asset — Responsibility and Terms</h2>\n" +
        "<p>Dear {{EmployeeName}},</p>\n" +
        "<p>The company asset described below has been issued to you under assignment " +
        "<strong>{{AssignmentNumber}}</strong> on {{AssignmentDate}}. Please read the declaration, " +
        "then sign and return this form.</p>\n" +
        "<table style='border-collapse:collapse;margin:1rem 0;font-size:0.95rem'>\n" +
        "<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Asset number</td><td>{{AssetNumber}}</td></tr>\n" +
        "<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Description</td><td>{{AssetName}}</td></tr>\n" +
        "{{#if AssetTypeName}}<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Type</td><td>{{AssetTypeName}}</td></tr>{{/if}}\n" +
        "{{#if SerialNumber}}<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Serial number</td><td>{{SerialNumber}}</td></tr>{{/if}}\n" +
        "{{#if Specifications}}<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Specifications</td><td>{{Specifications}}</td></tr>{{/if}}\n" +
        "<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Condition at issue</td><td>{{ConditionAtAssignment}}</td></tr>\n" +
        "<tr><td style='padding:3px 16px 3px 0;color:#6b7280'>Expected return</td><td>{{ExpectedReturnDate}}</td></tr>\n" +
        "</table>\n" +
        "<h3 style='font-size:0.95rem'>Declaration</h3>\n" +
        "<ol style='line-height:1.5'>\n" +
        "<li>I confirm that I have received the asset described above in {{ConditionAtAssignment}} " +
        "condition, and that it remains the property of {{CompanyName}} at all times.</li>\n" +
        "<li>{{#if ResponsibleForLoss}}I accept responsibility for the <strong>loss</strong> of this " +
        "asset while it is in my custody, and I understand that its value may be recovered from me in " +
        "accordance with company policy.{{else}}I am <strong>not</strong> held financially responsible " +
        "for the loss of this asset, without prejudice to my duty to take reasonable care of it." +
        "{{/if}}</li>\n" +
        "<li>{{#if ResponsibleForDamage}}I accept responsibility for <strong>damage</strong> to this " +
        "asset caused by misuse or negligence while it is in my custody, and I understand that the cost " +
        "of repair or replacement may be recovered from me in accordance with company policy." +
        "{{else}}I am <strong>not</strong> held financially responsible for damage to this asset, " +
        "without prejudice to my duty to take reasonable care of it.{{/if}}</li>\n" +
        "<li>I undertake to return this asset {{ReturnUndertaking}}.</li>\n" +
        "<li>I undertake to report any loss, theft or damage immediately, and not to lend, transfer or " +
        "dispose of this asset without written authorisation.</li>\n" +
        "</ol>\n" +
        "{{#if AdditionalTerms}}<h3 style='font-size:0.95rem'>Additional terms</h3>" +
        "<p style='white-space:pre-wrap'>{{AdditionalTerms}}</p>{{/if}}\n" +
        "{{#if AcknowledgedOn}}<p style='color:#6b7280;font-style:italic'>Receipt of this asset was " +
        "acknowledged in the staff portal on {{AcknowledgedOn}}.</p>{{/if}}\n" +
        "<table style='width:100%;margin-top:2.5rem;font-size:0.85rem'><tr>\n" +
        "<td style='width:45%;border-top:1px solid #111827;padding-top:6px'>" +
        "Employee signature and date<br/><span style='color:#6b7280'>{{EmployeeName}}</span></td>\n" +
        "<td style='width:10%'></td>\n" +
        "<td style='width:45%;border-top:1px solid #111827;padding-top:6px'>" +
        "Issuing officer signature and date<br/><span style='color:#6b7280'>{{SignatoryName}}" +
        "{{#if SignatoryTitle}}, {{SignatoryTitle}}{{/if}}</span></td>\n" +
        "</tr></table>\n" +
        "{{#if CompanyFooter}}<hr style='border:none;border-top:1px solid #e5e7eb;margin-top:2rem' />" +
        "<p style='font-size:0.75rem;color:#6b7280'>{{CompanyFooter}}</p>{{/if}}\n" +
        "</body></html>";

    private static IReadOnlyList<EmailEventDescriptor> Build() => new List<EmailEventDescriptor>
    {
        new()
        {
            Module = Module,
            EventKey = Events.ResponsibilityTerms,
            Name = "Asset Responsibility and Terms",
            Category = "Staff Assets",
            Description = "The form an employee signs when a company asset is issued to them "
                        + "(AST-5). Renders as a self-contained HTML document suitable for "
                        + "print-to-PDF and physical signature, and is the same document emailed to "
                        + "the holder (AST-5b). The liability clauses are conditional on the "
                        + "assignment's own responsibility flags.",
            DefaultSubject = "Company asset issued to you — {{AssetName}} ({{AssignmentNumber}})",
            DefaultHtmlBody = LetterShell,
            Tokens = new List<EmailTokenDescriptor>
            {
                new() { Token = "CompanyName",           Description = "Legal employer name",                       SampleValue = "Tema Development Company Ltd" },
                new() { Token = "CompanyAddress",        Description = "Registered address",                        SampleValue = "P. O. Box 46, Tema" },
                new() { Token = "CompanyLogoUrl",        Description = "Letterhead logo",                           SampleValue = "" },
                new() { Token = "CompanyFooter",         Description = "Document footer text",                      SampleValue = "" },
                new() { Token = "LetterDate",            Description = "Date the document is produced",             SampleValue = "24 August 2026" },
                new() { Token = "EmployeeName",          Description = "Employee the asset is issued to",           SampleValue = "Ama Mensah" },
                new() { Token = "EmployeeNumber",        Description = "Employee number",                           SampleValue = "TDC/0417" },
                new() { Token = "AssignmentNumber",      Description = "Assignment reference",                      SampleValue = "ASN-20260824-A1B2C3" },
                new() { Token = "AssignmentDate",        Description = "Date of issue",                             SampleValue = "24 August 2026" },
                new() { Token = "ExpectedReturnDate",    Description = "When it is due back, or 'On request / on exit'", SampleValue = "20 February 2027" },
                new() { Token = "AssetNumber",           Description = "Asset register number",                     SampleValue = "TDC-IT-0091" },
                new() { Token = "AssetName",             Description = "Asset description",                         SampleValue = "Dell Latitude 5440" },
                new() { Token = "AssetTypeName",         Description = "Asset type",                                SampleValue = "Laptop" },
                new() { Token = "SerialNumber",          Description = "Serial number, where recorded",             SampleValue = "SN-8842137" },
                new() { Token = "Specifications",        Description = "Specification notes",                       SampleValue = "16GB RAM, 512GB SSD" },
                new() { Token = "ConditionAtAssignment", Description = "Condition when issued",                     SampleValue = "Good" },
                new() { Token = "ResponsibleForLoss",    Description = "TRUE when the holder answers for loss — drives the conditional clause",   SampleValue = "true" },
                new() { Token = "ResponsibleForDamage",  Description = "TRUE when the holder answers for damage — drives the conditional clause", SampleValue = "true" },
                new() { Token = "ReturnUndertaking",     Description = "The return promise, worded from the due date", SampleValue = "on or before 20 February 2027, or on the day my employment ends, whichever is earlier" },
                new() { Token = "AdditionalTerms",       Description = "Free text carried from the assignment",     SampleValue = "Do not install unlicensed software." },
                new() { Token = "AcknowledgedOn",        Description = "Present only once the holder has acknowledged receipt", SampleValue = "" },
                new() { Token = "SignatoryName",         Description = "Issuing officer",                           SampleValue = "Head of Human Resources" },
                new() { Token = "SignatoryTitle",        Description = "Issuing officer's title",                   SampleValue = "Head of Human Resources" },
            },
        },
    };
}

/// <summary>DI-registered wrapper exposing the static <see cref="AssetsEmailCatalog"/>.</summary>
public sealed class AssetsEmailEventCatalog : IEmailEventCatalog
{
    public string Module => AssetsEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => AssetsEmailCatalog.All;
}
