using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// The printed recruitment test: the question paper a candidate writes on, and the marker's key —
/// round 4, lane E6.
/// </summary>
/// <remarks>
/// <para><b>Why a printed paper at all.</b> Decision D-2: the engine is sat online, and "a printable
/// paper covers offline sittings". A test centre with no computers, a candidate with no connection,
/// a practical sat at a desk — the paper is how those get the same questions, and the key is how the
/// marker applies the same rules the server applies online.</para>
///
/// <para><b>Templates, not a builder — the house pattern.</b> Lane F's
/// <see cref="InterviewPaperCatalog"/> records the reasoning: a document that is signed and filed is
/// the employer's to word, and <c>AssetTermsLetterService</c> once replaced a QuestPDF builder
/// precisely because it hard-coded wording "where nobody but a developer could change it". The
/// service composes the question block, which is structure; the template owns the language around
/// it, including the standing instructions to candidates and the marking rules on the key.</para>
///
/// <para>⚠ <b>The marking key reveals every answer.</b> It is served only from HR's own door, behind
/// the same recruitment read gate as the authoring screens that already show the key. A printed key
/// left in the examination room is the one failure no server check can prevent, which is why the key
/// says so on its face.</para>
/// </remarks>
public static class RecruitmentTestPaperCatalog
{
    public const string Module = "RecruitmentTests";

    public static class Events
    {
        /// <summary>What the candidate writes on. No answers anywhere on it.</summary>
        public const string QuestionPaper = "TestQuestionPaper";

        /// <summary>The marker's copy: correct choices, expected numbers, marking notes.</summary>
        public const string MarkingKey = "TestMarkingKey";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;
    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    public static EmailEventDescriptor? Find(string eventKey) =>
        All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>The letterhead. The corner label differs: the paper names itself, the key warns.</summary>
    private static string Head(string cornerLabel) =>
        "\n<html><body style='font-family:Georgia,serif;color:#111827;max-width:720px;margin:0 auto;padding:1.5rem'>\n" +
        "{{#if CompanyLogoUrl}}<img src='{{CompanyLogoUrl}}' alt='' style='height:48px;margin-bottom:0.75rem' />{{/if}}\n" +
        "<div style='display:flex;justify-content:space-between;align-items:flex-end;border-bottom:2px solid #111827;padding-bottom:0.5rem'>\n" +
        "  <div><strong style='font-size:1.05rem'>{{CompanyName}}</strong>" +
        "{{#if CompanyAddress}}<div style='font-size:0.75rem;color:#6b7280'>{{CompanyAddress}}</div>{{/if}}</div>\n" +
        $"  <div style='font-size:0.7rem;text-transform:uppercase;letter-spacing:0.06em;color:#6b7280'>{cornerLabel}</div>\n" +
        "</div>\n";

    private const string Foot = "\n</body></html>";

    private const string Label =
        "font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280";

    private static EmailTokenDescriptor T(string token, string desc, string sample) => new(token, desc, sample);

    /// <summary>A token the system fills with ready-made HTML — the only kind a template may place raw.</summary>
    private static EmailTokenDescriptor H(string token, string desc, string sample) => new(token, desc, sample) { IsHtml = true };

    private static List<EmailTokenDescriptor> CommonTokens() => new()
    {
        T("CompanyName", "The employer's name, from the company profile.", "Tema Development Corporation"),
        T("CompanyAddress", "Registered address for the letterhead.", "P.O. Box 46, Tema"),
        T("CompanyLogoUrl", "Letterhead logo. Omitted when none is configured.", ""),
        T("TestName", "The paper's name.", "Numerical Reasoning"),
        T("TestCode", "The paper's reference.", "TEST-0007"),
        T("DurationText", "Time allowed, in words.", "45 minutes"),
        T("QuestionCount", "How many questions are on the paper.", "20"),
        T("TotalPoints", "What a perfect script scores.", "40"),
        T("Instructions", "The paper's own instructions; the box is hidden when it has none.",
          "Answer every question. Calculators are permitted."),
    };

    private static List<EmailEventDescriptor> Build()
    {
        var list = new List<EmailEventDescriptor>();

        // ── 1. The question paper ──────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.QuestionPaper,
            Name = "Recruitment Test Paper (printed)",
            Category = "Assessment",
            Description =
                "What a candidate writes on at a test centre: every question, with tick boxes and "
                + "answer lines, and no answers anywhere on it. Printed blank, or one named paper per "
                + "candidate a test assignment reaches. Never shuffled — one marking key has to fit "
                + "every script in the pile.",
            DefaultSubject = "{{TestName}} ({{TestCode}})",
            DefaultHtmlBody = Head("{{TestCode}}") +
                $"<div style='{Label};margin-top:0.75rem'>Recruitment Assessment</div>\n" +
                "<h1 style='font-size:1.3rem;margin:0.15rem 0 0'>{{TestName}}</h1>\n" +
                "<div style='display:grid;grid-template-columns:repeat(3,1fr);gap:0.35rem 0.75rem;font-size:0.8rem;margin:0.75rem 0 1rem'>\n" +
                $"  <div><div style='{Label}'>Time allowed</div><strong>{{{{DurationText}}}}</strong></div>\n" +
                $"  <div><div style='{Label}'>Questions</div><strong>{{{{QuestionCount}}}}</strong></div>\n" +
                $"  <div><div style='{Label}'>Total marks</div><strong>{{{{TotalPoints}}}}</strong></div>\n" +
                "</div>\n" +
                "<div class='interview-paper-keep' style='border:1px solid #d1d5db;padding:0.6rem 0.75rem;font-size:0.85rem;display:grid;grid-template-columns:1fr 1fr;gap:0.55rem 1.25rem'>\n" +
                "  <div><span style='color:#6b7280'>Name:</span> {{#if CandidateName}}<strong>{{CandidateName}}</strong>{{else}}______________________________{{/if}}</div>\n" +
                "  <div><span style='color:#6b7280'>Application ref:</span> {{#if ApplicationNumber}}<strong>{{ApplicationNumber}}</strong>{{else}}________________{{/if}}</div>\n" +
                "  <div><span style='color:#6b7280'>Role applied for:</span> {{#if JobTitle}}{{JobTitle}}{{else}}________________________{{/if}}</div>\n" +
                "  <div><span style='color:#6b7280'>Date:</span> ________________</div>\n" +
                "  <div style='grid-column:1 / span 2'><span style='color:#6b7280'>Signature:</span> ______________________________</div>\n" +
                "</div>\n" +
                "{{#if Instructions}}<div style='background:#f9fafb;border:1px solid #e5e7eb;padding:0.6rem 0.75rem;margin-top:0.75rem;font-size:0.85rem;white-space:pre-wrap'>{{Instructions}}</div>{{/if}}\n" +
                "<p style='font-size:0.8rem;color:#374151;margin:0.75rem 0 0'>\n" +
                "  Write clearly, in ink. Where a question asks for one answer, tick one box; where it asks\n" +
                "  for every answer that applies, tick all of them. A question left blank scores nothing.\n" +
                "</p>\n" +
                "{{{QuestionBlock}}}\n" +
                "<div class='interview-paper-keep' style='margin-top:1.5rem;border-top:2px solid #111827;padding-top:0.5rem;font-size:0.8rem;display:grid;grid-template-columns:repeat(3,1fr);gap:1rem'>\n" +
                $"  <div><div style='{Label}'>For official use — marks</div>______ / {{{{TotalPoints}}}}</div>\n" +
                $"  <div><div style='{Label}'>Marked by</div>____________________</div>\n" +
                $"  <div><div style='{Label}'>Date</div>______________</div>\n" +
                "</div>\n" + Foot,
            Tokens = CommonTokens().Concat(new[]
            {
                T("CandidateName", "The candidate the paper is printed for; a blank line on a blank paper.", "Ada Boahen"),
                T("ApplicationNumber", "Their application reference; a blank line on a blank paper.", "APP-000456"),
                T("JobTitle", "The role they applied for; a blank line on a blank paper.", "Senior Accountant"),
                H("QuestionBlock", "⚠ Raw HTML — every question with its tick boxes or answer lines. No answers.",
                  "<div>…</div>"),
            }).ToList(),
        });

        // ── 2. The marking key ─────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.MarkingKey,
            Name = "Recruitment Test Marking Key (printed)",
            Category = "Assessment",
            Description =
                "The marker's copy of the paper: the correct choices, the expected numeric answers, "
                + "the marking note for each written answer, and the rules the server applies online. "
                + "⚠ It reveals every answer — keep it out of the examination room.",
            DefaultSubject = "Marking key — {{TestName}} ({{TestCode}})",
            DefaultHtmlBody = Head("Confidential — marking key") +
                $"<div style='{Label};margin-top:0.75rem'>Marking Key</div>\n" +
                "<h1 style='font-size:1.3rem;margin:0.15rem 0 0'>{{TestName}}</h1>\n" +
                "<div style='display:grid;grid-template-columns:repeat(4,1fr);gap:0.35rem 0.75rem;font-size:0.8rem;margin:0.75rem 0 1rem'>\n" +
                $"  <div><div style='{Label}'>Total marks</div><strong>{{{{TotalPoints}}}}</strong></div>\n" +
                $"  <div><div style='{Label}'>Marked by the key</div><strong>{{{{AutoMarkablePoints}}}}</strong></div>\n" +
                $"  <div><div style='{Label}'>Marked by hand</div><strong>{{{{WrittenPoints}}}}</strong></div>\n" +
                $"  <div><div style='{Label}'>Pass mark</div><strong>{{{{PassMarkText}}}}</strong></div>\n" +
                "</div>\n" +
                "<div class='interview-paper-keep' style='background:#fffbeb;border:1px solid #fde68a;padding:0.6rem 0.75rem;font-size:0.8rem;color:#78350f'>\n" +
                "  <strong>How this paper is marked</strong> — the same rules the system applies to a paper sat online.\n" +
                "  <ul style='margin:0.35rem 0 0 1.1rem;padding:0'>\n" +
                "    <li>A question asking for every answer that applies scores only when every correct box and no wrong box is ticked. There is no part credit.</li>\n" +
                "    <li>A numeric answer is compared as a number: 7, 7.0 and 7.00 are the same answer.</li>\n" +
                "    <li>A question left blank scores nothing, and still counts towards the total.</li>\n" +
                "    <li>A written answer is marked by hand, from nothing up to the marks shown against it.</li>\n" +
                "  </ul>\n" +
                "</div>\n" +
                "{{{KeyBlock}}}\n" +
                "<p style='color:#9ca3af;font-size:0.72rem;margin-top:1.5rem'>\n" +
                "  Keep this key apart from the scripts, and never leave it in the examination room.\n" +
                "</p>\n" + Foot,
            Tokens = CommonTokens().Concat(new[]
            {
                T("AutoMarkablePoints", "Marks the key settles: every closed question.", "34"),
                T("WrittenPoints", "Marks awarded by hand: the written answers.", "6"),
                T("PassMarkText", "The pass mark, or 'none set'.", "50%"),
                H("KeyBlock", "⚠ Raw HTML — each question with its correct answer and marking note.",
                  "<table>…</table>"),
            }).ToList(),
        });

        return list;
    }
}

/// <summary>Registers <see cref="RecruitmentTestPaperCatalog"/> with the templated-email service.</summary>
public sealed class RecruitmentTestPaperEmailEventCatalog : IEmailEventCatalog
{
    public string Module => RecruitmentTestPaperCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => RecruitmentTestPaperCatalog.All;
}
