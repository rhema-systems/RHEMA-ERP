using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// The printed interview paper: the panel's scoring sheet and the question list — round 4, lane F.
/// </summary>
/// <remarks>
/// <para><b>This is a port, and the original is worth knowing about.</b> The solution HR was ported
/// from carried a scorecard at
/// <c>ErpSystem.BlazorServer\Pages\Recruitment\InterviewScorecard.razor</c> — one sheet per
/// candidate, a question table with an empty score box and two ruled remark lines, section totals,
/// five recommendation tick boxes, comment lines and a signature block. <b>It was never brought
/// across</b>, and the recruitment guide's § 9 never noticed, because a missing print view looks
/// exactly like a print view nobody asked for.</para>
///
/// <para><b>Seven things that original got wrong, all fixed here.</b> It loaded <c>Weight</c> and
/// never printed it, so the Section Total box had no denominator and an offline marker could not
/// weight anything. It printed only the CURRENT panelist although it had loaded the whole panel.
/// It ignored external panelists entirely. It had no letterhead — the only branding was the word
/// "Confidential". Candidate context was the name alone. Every word of it lived in inline HTML in
/// the page. And there was nowhere to say what a good answer sounds like.</para>
///
/// <para><b>Why a template and not a builder.</b> A scoring sheet is signed and filed; its wording
/// is the employer's. <c>AssetTermsLetterService</c> records this module reversing the opposite
/// decision once already — a QuestPDF builder was replaced precisely because it hard-coded wording
/// in C# "where nobody but a developer could change it". The service composes the tables, which are
/// structure; the template owns the language.</para>
/// </remarks>
public static class InterviewPaperCatalog
{
    public const string Module = "Interviews";

    public static class Events
    {
        /// <summary>One candidate, one panelist: the sheet that gets marked and signed.</summary>
        public const string ScoreSheet = "InterviewScoreSheet";

        /// <summary>The drawn questions alone, for the panel's own preparation. No score boxes.</summary>
        public const string QuestionList = "InterviewQuestionList";

        /// <summary>The cover page: who is on the panel, and the day's timetable.</summary>
        public const string PackCover = "InterviewPackCover";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;
    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    public static EmailEventDescriptor? Find(string eventKey) =>
        All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The letterhead every sheet opens with. Kept in one place so a tenant rewording the scoring
    /// sheet does not end up with a different letterhead on the question list.
    /// </summary>
    private const string Head =
        "\n<html><body style='font-family:Georgia,serif;color:#111827;max-width:720px;margin:0 auto;padding:1.5rem'>\n" +
        "{{#if CompanyLogoUrl}}<img src='{{CompanyLogoUrl}}' alt='' style='height:48px;margin-bottom:0.75rem' />{{/if}}\n" +
        "<div style='display:flex;justify-content:space-between;align-items:flex-end;border-bottom:2px solid #111827;padding-bottom:0.5rem'>\n" +
        "  <div><strong style='font-size:1.05rem'>{{CompanyName}}</strong>" +
        "{{#if CompanyAddress}}<div style='font-size:0.75rem;color:#6b7280'>{{CompanyAddress}}</div>{{/if}}</div>\n" +
        "  <div style='font-size:0.7rem;text-transform:uppercase;letter-spacing:0.06em;color:#6b7280'>Confidential</div>\n" +
        "</div>\n";

    private const string Foot = "\n</body></html>";

    /// <summary>The session's own facts, printed identically at the top of every variant.</summary>
    private const string MetaGrid =
        "<div style='display:grid;grid-template-columns:repeat(3,1fr);gap:0.35rem 0.75rem;font-size:0.8rem;margin:0.75rem 0 1rem'>\n" +
        "  <div><div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Interview</div><strong>{{InterviewNumber}}</strong></div>\n" +
        "  <div><div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Date</div><strong>{{InterviewDate}}</strong></div>\n" +
        "  <div><div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Time</div><strong>{{TimeRange}}</strong></div>\n" +
        "  <div><div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Vacancy</div><strong>{{VacancyNumber}}</strong></div>\n" +
        "  <div><div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Round</div><strong>Round {{Round}}</strong></div>\n" +
        "  <div><div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Format</div><strong>{{InterviewType}}</strong></div>\n" +
        "</div>\n";

    private static EmailTokenDescriptor T(string token, string desc, string sample) => new(token, desc, sample);

    /// <summary>A token the system fills with ready-made HTML — the only kind a template may place raw.</summary>
    private static EmailTokenDescriptor H(string token, string desc, string sample) => new(token, desc, sample) { IsHtml = true };

    /// <summary>The session facts every variant shares.</summary>
    private static List<EmailTokenDescriptor> SessionTokens() => new()
    {
        T("CompanyName", "The employer's name, from the company profile.", "Tema Development Corporation"),
        T("CompanyAddress", "Registered address for the letterhead.", "P.O. Box 46, Tema"),
        T("CompanyLogoUrl", "Letterhead logo. Omitted when none is configured.", ""),
        T("JobTitle", "The role being interviewed for.", "Senior Accountant"),
        T("InterviewNumber", "Reference of this session.", "INT-2026-00042"),
        T("InterviewDate", "Day of the session.", "22 September 2026"),
        T("TimeRange", "The session window.", "09:00 – 11:00"),
        T("VacancyNumber", "Reference of the vacancy.", "VAC-000123"),
        T("Round", "Which round this is.", "1"),
        T("InterviewType", "Panel, one-to-one, technical…", "Panel"),
        H("PanelTable", "⚠ Raw HTML — the whole panel, internal and external, with roles.", "<table>…</table>"),
    };

    private static List<EmailEventDescriptor> Build()
    {
        var list = new List<EmailEventDescriptor>();

        // ── 1. The scoring sheet ───────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.ScoreSheet,
            Name = "Interview Scoring Sheet (printed)",
            Category = "Interview",
            Description =
                "One candidate, one panelist. Printed, marked by hand in the room, signed, and the "
                + "marks typed back in afterwards. The question table carries each question's weight "
                + "and score band, because a sheet that omits them cannot be marked correctly.",
            DefaultSubject = "Interview scoring sheet — {{CandidateName}} ({{InterviewNumber}})",
            DefaultHtmlBody = Head +
                "<div style='font-size:0.7rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280;margin-top:0.75rem'>Interview Evaluation Scorecard</div>\n" +
                "<h1 style='font-size:1.25rem;margin:0.15rem 0 0'>{{JobTitle}}</h1>\n" +
                MetaGrid +
                "<div style='display:flex;gap:1rem;background:#f9fafb;border:1px solid #e5e7eb;padding:0.5rem 0.75rem;font-size:0.85rem'>\n" +
                "  <div><span style='color:#6b7280'>Candidate:</span> <strong>{{CandidateName}}</strong></div>\n" +
                "  {{#if ApplicationNumber}}<div><span style='color:#6b7280'>Ref:</span> {{ApplicationNumber}}</div>{{/if}}\n" +
                "  {{#if SlotTime}}<div><span style='color:#6b7280'>Seen at:</span> <strong>{{SlotTime}}</strong></div>{{/if}}\n" +
                "</div>\n" +
                "{{#if CandidateContext}}<div style='font-size:0.78rem;color:#4b5563;margin-top:0.35rem'>{{CandidateContext}}</div>{{/if}}\n" +
                "<div style='display:flex;gap:0.5rem;align-items:center;background:#fff;border:1px solid #e5e7eb;border-top:none;padding:0.45rem 0.75rem;font-size:0.85rem'>\n" +
                "  <span style='color:#6b7280'>Panelist:</span> <strong>{{PanelistName}}</strong>\n" +
                "  {{#if PanelistRole}}<span style='background:#e5e7eb;border-radius:4px;padding:1px 6px;font-size:0.7rem'>{{PanelistRole}}</span>{{/if}}\n" +
                "</div>\n" +
                "{{{QuestionTable}}}\n" +
                "<div class='interview-paper-keep' style='margin-top:1rem'>\n" +
                "  <div style='font-size:0.75rem;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;color:#374151'>Recommendation</div>\n" +
                "  <div style='display:flex;gap:0.5rem;flex-wrap:wrap;margin-top:0.35rem'>{{{RecommendationBoxes}}}</div>\n" +
                "</div>\n" +
                "<div class='interview-paper-keep' style='margin-top:0.9rem'>\n" +
                "  <div style='font-size:0.75rem;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;color:#374151'>General comments and observations</div>\n" +
                "  {{{CommentLines}}}\n" +
                "</div>\n" +
                "<div class='interview-paper-keep' style='display:grid;grid-template-columns:1fr 1fr;gap:1.5rem;margin-top:1.5rem'>\n" +
                "  <div><div style='border-bottom:1px solid #111827;height:1.5rem'></div>" +
                "<div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Panelist signature</div></div>\n" +
                "  <div><div style='border-bottom:1px solid #111827;height:1.5rem'></div>" +
                "<div style='font-size:0.65rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280'>Date</div></div>\n" +
                "</div>\n" + Foot,
            Tokens = SessionTokens().Concat(new[]
            {
                T("CandidateName", "Who is being interviewed.", "Ada Boahen"),
                T("ApplicationNumber", "Their application reference.", "APP-000456"),
                T("SlotTime", "Their slot, when the day was apportioned.", "09:30 – 10:00"),
                T("CandidateContext", "Current role and experience, so the panel has context.",
                  "Currently Accountant at Blue Ltd · 6 years' experience"),
                T("PanelistName", "Whose sheet this is. A blank rule when printed unassigned.", "Kofi Mensah"),
                T("PanelistRole", "Chair, member, subject expert…", "Chair"),
                H("QuestionTable", "⚠ Raw HTML — the questions with weight, band, score box and remark lines.",
                  "<table>…</table>"),
                H("RecommendationBoxes", "⚠ Raw HTML — the five tick boxes.", "<span>…</span>"),
                H("CommentLines", "⚠ Raw HTML — the ruled lines for written comments.", "<div>…</div>"),
            }).ToList(),
        });

        // ── 2. The question list ───────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.QuestionList,
            Name = "Interview Question List (printed)",
            Category = "Interview",
            Description =
                "The drawn questions alone, for the panel to read beforehand. No score boxes — this "
                + "is preparation, not a record. ⚠ It reveals the questions, so it stays behind the "
                + "same per-record gate as everything else on the interview.",
            DefaultSubject = "Interview questions — {{InterviewNumber}}",
            DefaultHtmlBody = Head +
                "<div style='font-size:0.7rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280;margin-top:0.75rem'>Interview Question List</div>\n" +
                "<h1 style='font-size:1.25rem;margin:0.15rem 0 0'>{{JobTitle}}</h1>\n" +
                MetaGrid +
                "{{{PanelTable}}}\n" +
                "{{{QuestionTable}}}\n" +
                "<p style='color:#9ca3af;font-size:0.72rem;margin-top:1.5rem'>\n" +
                "  For the panel's preparation. Do not share with candidates.\n" +
                "</p>\n" + Foot,
            Tokens = SessionTokens().Concat(new[]
            {
                H("QuestionTable", "⚠ Raw HTML — the questions with their weight and band, no score boxes.",
                  "<table>…</table>"),
            }).ToList(),
        });

        // ── 3. The pack cover ──────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.PackCover,
            Name = "Interview Pack Cover (printed)",
            Category = "Interview",
            Description =
                "The first page of a full pack: who is on the panel, and the day's timetable. The "
                + "scoring sheets follow it, one per candidate per panelist.",
            DefaultSubject = "Interview pack — {{InterviewNumber}}",
            DefaultHtmlBody = Head +
                "<div style='font-size:0.7rem;text-transform:uppercase;letter-spacing:0.05em;color:#6b7280;margin-top:0.75rem'>Interview Pack</div>\n" +
                "<h1 style='font-size:1.25rem;margin:0.15rem 0 0'>{{JobTitle}}</h1>\n" +
                MetaGrid +
                "{{#if LocationOrLink}}<div style='font-size:0.85rem;margin-bottom:0.75rem'>" +
                "<span style='color:#6b7280'>Where:</span> {{LocationOrLink}}</div>{{/if}}\n" +
                "<div style='font-size:0.75rem;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;color:#374151;margin-top:0.5rem'>The panel</div>\n" +
                "{{{PanelTable}}}\n" +
                "<div style='font-size:0.75rem;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;color:#374151;margin-top:1rem'>The day</div>\n" +
                "{{{TimetableTable}}}\n" +
                "{{#if Instructions}}<div style='margin-top:1rem;font-size:0.82rem'>" +
                "<div style='color:#6b7280'>Instructions</div>{{Instructions}}</div>{{/if}}\n" + Foot,
            Tokens = SessionTokens().Concat(new[]
            {
                T("LocationOrLink", "Room or joining link.", "Boardroom, Head Office"),
                T("Instructions", "Anything the panel was told when the session was scheduled.", ""),
                H("TimetableTable", "⚠ Raw HTML — each candidate and their slot.", "<table>…</table>"),
            }).ToList(),
        });

        return list;
    }
}

/// <summary>Registers <see cref="InterviewPaperCatalog"/> with the templated-email service.</summary>
public sealed class InterviewPaperEmailEventCatalog : IEmailEventCatalog
{
    public string Module => InterviewPaperCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => InterviewPaperCatalog.All;
}
