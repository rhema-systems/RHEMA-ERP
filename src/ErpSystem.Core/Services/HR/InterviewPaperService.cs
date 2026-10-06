using System.Text;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Builds the printed interview paper — the panel's scoring sheet, the question list, or the whole
/// pack — and renders it through the HR-editable <c>Interviews</c> templates. Round 4, lane F.
/// </summary>
/// <remarks>
/// <para><b>Structure here, language in the template.</b> This service composes the tables — which
/// questions, which weights, which bands — because those are facts about the interview. What the
/// sheet SAYS around them belongs to whoever signs it, so it lives in
/// <see cref="InterviewPaperCatalog"/> where HR can reword it without a deployment. The same split
/// <c>OfferLetterService</c> and <c>ProbationLetterService</c> already make.</para>
///
/// <para><b>No PDF.</b> A scoring sheet is printed, written on and signed, so the browser's own
/// print is the target. <c>AssetTermsLetterService</c> records this module reversing the opposite
/// decision once already.</para>
/// </remarks>
public sealed class InterviewPaperService : IInterviewPaperService
{
    private readonly IJobInterviewRepository _interviewRepository;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<InterviewPaperService> _logger;

    public InterviewPaperService(
        IJobInterviewRepository interviewRepository,
        ITemplatedEmailService templatedEmail,
        ICompanyProfileProvider companyProfile,
        ICurrentUserProvider currentUserProvider,
        ILogger<InterviewPaperService> logger)
    {
        _interviewRepository = interviewRepository;
        _templatedEmail = templatedEmail;
        _companyProfile = companyProfile;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <inheritdoc />
    public async Task<InterviewPaperDto> GenerateAsync(
        Guid interviewId,
        InterviewPaperVariant variant,
        Guid? panelistId,
        IReadOnlyList<Guid>? intervieweeIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var interview = await _interviewRepository.GetWithFullDetailsAsync(interviewId);
        if (interview is null || interview.TenantId != tenantId)
            throw new ArgumentException($"Interview '{interviewId}' not found.");

        // ⚠ Filtered in memory, not re-queried. `GetWithFullDetailsAsync` already includes
        // ExternalPanelists with their associate — but an EF `Include` on a collection does not
        // apply the soft-delete filter, so a removed assessor comes back attached and would be
        // handed a sheet for a panel they are no longer on.
        //
        // The ported original omitted external panelists entirely: it read `iv.Panelists` only, so
        // an external assessor got no sheet at all.
        var externals = (interview.ExternalPanelists ?? new List<JobInterviewExternalPanelist>())
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToList();

        var company = await _companyProfile.GetAsync(cancellationToken);

        var sessionTokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = string.IsNullOrWhiteSpace(company.LegalName) ? "Our Company" : company.LegalName,
            ["CompanyAddress"] = company.RegisteredAddress,
            ["CompanyLogoUrl"] = await _companyProfile.GetLogoAsync(company.TenantId, cancellationToken), // lane 4c (F-55)
            ["JobTitle"] = interview.JobVacancy?.JobTitle ?? "Interview",
            ["InterviewNumber"] = interview.InterviewNumber,
            ["InterviewDate"] = interview.ScheduledDate.ToString("dd MMMM yyyy"),
            ["TimeRange"] = $"{interview.StartTime:hh\\:mm} – {interview.EndTime:hh\\:mm}",
            ["VacancyNumber"] = interview.JobVacancy?.VacancyNumber ?? string.Empty,
            ["Round"] = interview.Round.ToString(),
            ["InterviewType"] = Prettify(interview.Type.ToString()),
            ["PanelTable"] = BuildPanelTable(interview, externals),
        };

        var attendees = SelectAttendees(interview, intervieweeIds);

        var html = variant switch
        {
            InterviewPaperVariant.Questions => await RenderQuestionListAsync(interview, sessionTokens, cancellationToken),
            InterviewPaperVariant.Pack => await RenderPackAsync(interview, externals, attendees, sessionTokens, cancellationToken),
            _ => Join(await RenderScoreSheetsAsync(
                interview, externals, attendees, panelistId, sessionTokens, cancellationToken)),
        };

        return new InterviewPaperDto
        {
            InterviewId = interview.Id,
            InterviewNumber = interview.InterviewNumber,
            JobTitle = interview.JobVacancy?.JobTitle ?? "Interview",
            Variant = variant,
            SheetCount = CountSheets(html),
            HtmlBody = html,
        };
    }

    // ── variants ────────────────────────────────────────────────────────────────

    private async Task<string> RenderQuestionListAsync(
        JobInterview interview, Dictionary<string, string?> session, CancellationToken ct)
    {
        var tokens = new Dictionary<string, string?>(session, StringComparer.OrdinalIgnoreCase)
        {
            ["QuestionTable"] = BuildQuestionTable(interview, withScoreBoxes: false),
        };

        // ⚠ Joined even though it is one sheet. Every variant must come back wrapped in a
        // `.interview-paper-sheet` section: the print stylesheet hangs its page breaks on that
        // class, and `SheetCount` counts it. An unwrapped single sheet reports a count of zero,
        // which the screen renders as "nothing to print" over a page that plainly has something.
        return Join(new[] { await RenderAsync(InterviewPaperCatalog.Events.QuestionList, tokens, ct) });
    }

    private async Task<string> RenderPackAsync(
        JobInterview interview,
        IReadOnlyList<JobInterviewExternalPanelist> externals,
        IReadOnlyList<JobInterviewee> attendees,
        Dictionary<string, string?> session,
        CancellationToken ct)
    {
        var coverTokens = new Dictionary<string, string?>(session, StringComparer.OrdinalIgnoreCase)
        {
            ["LocationOrLink"] = interview.LocationOrLink,
            ["Instructions"] = interview.Instructions,
            ["TimetableTable"] = BuildTimetable(attendees),
        };

        var cover = await RenderAsync(InterviewPaperCatalog.Events.PackCover, coverTokens, ct);
        var sheets = await RenderScoreSheetsAsync(interview, externals, attendees, null, session, ct);

        // The cover is the pack's first sheet, not a wrapper around it.
        return Join(new[] { cover }.Concat(sheets).ToList());
    }

    /// <summary>
    /// One sheet per (candidate × panelist), concatenated with page breaks.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>The template renders ONE sheet and this loops it</b>, rather than the template
    /// carrying a repeated blob. That is what makes the wording editable: HR rewords a scoring
    /// sheet, not a document that happens to contain nine of them.</para>
    ///
    /// <para>With no panelist named, every panelist gets a sheet for every candidate — which is what
    /// a pack is. The ported original printed only the one current panelist even though it had
    /// loaded the whole panel, so a five-person panel needed five separate print runs.</para>
    /// </remarks>
    /// <returns>
    /// The sheet <b>bodies</b>, unwrapped. ⚠ Returning HTML that was already through
    /// <see cref="Join"/> is what made the pack nest a section inside a section: the pack joined
    /// [cover, sheets] and wrapped six already-wrapped sheets in a seventh, so the page break fell
    /// in the wrong place and the sheet count read eight. One join, at the end, by the caller.
    /// </returns>
    private async Task<List<string>> RenderScoreSheetsAsync(
        JobInterview interview,
        IReadOnlyList<JobInterviewExternalPanelist> externals,
        IReadOnlyList<JobInterviewee> attendees,
        Guid? panelistId,
        Dictionary<string, string?> session,
        CancellationToken ct)
    {
        var panel = BuildPanelList(interview, externals);
        if (panelistId is { } only)
            panel = panel.Where(p => p.Id == only).ToList();

        // No panel yet, or a filter that matched nobody: still print, with a blank rule where the
        // name goes. A sheet that cannot be printed until the panel is finalised is a sheet nobody
        // can prepare with.
        if (panel.Count == 0)
            panel = new List<PanelMember> { new(Guid.Empty, null, null) };

        // Likewise no candidates: one blank sheet rather than an empty document.
        var subjects = attendees.Count > 0
            ? attendees.Select(a => (JobInterviewee?)a).ToList()
            : new List<JobInterviewee?> { null };

        var questionTable = BuildQuestionTable(interview, withScoreBoxes: true);
        var recommendations = BuildRecommendationBoxes();
        var comments = BuildCommentLines(4);

        var sheets = new List<string>();
        foreach (var subject in subjects)
        {
            foreach (var member in panel)
            {
                var tokens = new Dictionary<string, string?>(session, StringComparer.OrdinalIgnoreCase)
                {
                    ["CandidateName"] = subject?.JobApplication?.JobCandidate?.FullName ?? BlankRule,
                    ["ApplicationNumber"] = subject?.JobApplication?.ApplicationNumber,
                    ["SlotTime"] = subject is { SlotStartTime: { } s, SlotEndTime: { } e }
                        ? $"{s:hh\\:mm} – {e:hh\\:mm}"
                        : null,
                    ["CandidateContext"] = BuildCandidateContext(subject),
                    ["PanelistName"] = member.Name ?? BlankRule,
                    ["PanelistRole"] = member.Role,
                    ["QuestionTable"] = questionTable,
                    ["RecommendationBoxes"] = recommendations,
                    ["CommentLines"] = comments,
                };
                sheets.Add(await RenderAsync(InterviewPaperCatalog.Events.ScoreSheet, tokens, ct));
            }
        }

        return sheets;
    }

    private async Task<string> RenderAsync(
        string eventKey, IReadOnlyDictionary<string, string?> tokens, CancellationToken ct)
    {
        var rendered = await _templatedEmail.RenderAsync(InterviewPaperCatalog.Module, eventKey, tokens, ct);
        if (rendered is null)
            throw new InvalidOperationException(
                $"No '{eventKey}' template is available. The shipped default should always resolve — "
                + "check that the Interviews catalogue is registered.");
        return rendered.HtmlBody;
    }

    // ── HTML fragments (every value HTML-encoded before it goes in) ──────────────

    private const string BlankRule = "______________________________";

    /// <summary>
    /// The question table — and the reason this whole lane exists rather than a simple port.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Weight and the score band are both printed.</b> The server scores an answer as
    /// <c>(raw ÷ MaxScore) × Weight</c>, so a question marked out of 5 with weight 20 counts exactly
    /// as much as one marked out of 10 with weight 20. A sheet that shows the box but not the band
    /// and the weight cannot be marked correctly, and the section total has no denominator. The
    /// ported original loaded <c>Weight</c> and never printed it.
    /// </remarks>
    private static string BuildQuestionTable(JobInterview interview, bool withScoreBoxes)
    {
        var plans = (interview.Questions ?? new List<JobInterviewQuestion>())
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.DisplayOrder)
            .ToList();

        if (plans.Count == 0)
            return "<p style='font-size:0.85rem;color:#6b7280;margin-top:1rem'>"
                 + "No questions have been drawn for this interview yet.</p>";

        var sb = new StringBuilder();
        var achievableTotal = 0;

        foreach (var plan in plans)
        {
            var questions = (plan.SelectedQuestions ?? new List<JobInterviewSelectedQuestion>())
                .Where(q => !q.IsDeleted)
                .OrderBy(q => q.DisplayOrder)
                .ToList();
            if (questions.Count == 0) continue;

            var sectionWeight = questions.Sum(q => q.Question?.Weight ?? 0);
            achievableTotal += sectionWeight;

            sb.Append("<div style='font-size:0.72rem;font-weight:700;text-transform:uppercase;")
              .Append("letter-spacing:0.06em;color:#374151;margin:0.9rem 0 0.3rem;")
              .Append("border-bottom:1px solid #e5e7eb;padding-bottom:0.2rem'>")
              .Append(Encode(plan.QuestionType?.TypeName ?? "Questions"))
              .Append("</div>\n<table style='width:100%;border-collapse:collapse;font-size:0.8rem'>\n<thead><tr>")
              .Append(Th("#", "width:22px")).Append(Th("Question", string.Empty))
              .Append(Th("Weight", "width:52px;text-align:center"))
              .Append(Th("Scale", "width:52px;text-align:center"));
            if (withScoreBoxes)
                sb.Append(Th("Score", "width:56px;text-align:center")).Append(Th("Remarks", "min-width:140px"));
            sb.Append("</tr></thead>\n<tbody>\n");

            var n = 1;
            foreach (var q in questions)
            {
                var detail = q.Question;
                sb.Append("<tr>")
                  .Append(Td(n.ToString(), "color:#9ca3af;font-size:0.72rem;vertical-align:top"))
                  .Append("<td style='padding:0.35rem 0.4rem;border-bottom:1px solid #f3f4f6;vertical-align:top'>")
                  .Append(Encode(detail?.QuestionText ?? string.Empty));

                // Printed only when set — an empty "What to listen for" heading on every row would
                // be noise on a sheet that has to stay readable at a glance.
                if (!string.IsNullOrWhiteSpace(detail?.ScoringGuide))
                    sb.Append("<div style='font-size:0.7rem;color:#6b7280;margin-top:0.15rem'><em>")
                      .Append(Encode(detail!.ScoringGuide!)).Append("</em></div>");

                sb.Append("</td>")
                  .Append(Td((detail?.Weight ?? 0).ToString(), "text-align:center;vertical-align:top"))
                  .Append(Td($"{detail?.MinScore ?? 0}–{detail?.MaxScore ?? 0}",
                             "text-align:center;color:#6b7280;font-size:0.75rem;vertical-align:top"));

                if (withScoreBoxes)
                {
                    sb.Append("<td style='padding:0.35rem 0.4rem;border-bottom:1px solid #f3f4f6;text-align:center'>")
                      .Append("<span style='display:inline-block;width:40px;height:20px;border:1.5px solid #374151;border-radius:3px'></span>")
                      .Append("</td>")
                      .Append("<td style='padding:0.35rem 0.4rem;border-bottom:1px solid #f3f4f6'>")
                      .Append("<div style='border-bottom:1px solid #9ca3af;height:13px'></div>")
                      .Append("<div style='border-bottom:1px solid #9ca3af;height:13px;margin-top:3px'></div>")
                      .Append("</td>");
                }
                sb.Append("</tr>\n");
                n++;
            }

            if (withScoreBoxes)
            {
                sb.Append("<tr><td colspan='3' style='text-align:right;padding:0.35rem 0.5rem;")
                  .Append("border-top:2px solid #374151;font-weight:600;font-size:0.78rem'>Section total</td>")
                  .Append("<td style='border-top:2px solid #374151;text-align:center;font-size:0.72rem;color:#6b7280'>")
                  .Append("/ ").Append(sectionWeight).Append("</td>")
                  .Append("<td style='border-top:2px solid #374151;text-align:center'>")
                  .Append("<span style='display:inline-block;width:40px;height:20px;border:1.5px solid #374151;border-radius:3px'></span>")
                  .Append("</td><td style='border-top:2px solid #374151'></td></tr>\n");
            }
            sb.Append("</tbody></table>\n");
        }

        if (withScoreBoxes && achievableTotal > 0)
            sb.Append("<div style='text-align:right;margin-top:0.5rem;font-size:0.85rem;font-weight:700'>")
              .Append("Weighted total &nbsp;<span style='display:inline-block;width:52px;height:22px;")
              .Append("border:1.5px solid #111827;border-radius:3px;vertical-align:middle'></span>")
              .Append("<span style='color:#6b7280;font-weight:400'> / ").Append(achievableTotal).Append("</span></div>\n");

        return sb.ToString();
    }

    /// <summary>The whole panel — internal and external — which the ported original never printed.</summary>
    private static string BuildPanelTable(
        JobInterview interview, IReadOnlyList<JobInterviewExternalPanelist> externals)
    {
        var members = BuildPanelList(interview, externals);
        if (members.Count == 0)
            return "<p style='font-size:0.82rem;color:#6b7280'>No panel has been named yet.</p>";

        var sb = new StringBuilder("<table style='width:100%;border-collapse:collapse;font-size:0.82rem'>\n");
        foreach (var m in members)
        {
            sb.Append("<tr><td style='padding:0.25rem 0.4rem;border-bottom:1px solid #f3f4f6'>")
              .Append(Encode(m.Name ?? BlankRule)).Append("</td>")
              .Append("<td style='padding:0.25rem 0.4rem;border-bottom:1px solid #f3f4f6;color:#6b7280;width:40%'>")
              .Append(Encode(m.Role ?? string.Empty)).Append("</td></tr>\n");
        }
        return sb.Append("</table>\n").ToString();
    }

    private static string BuildTimetable(IReadOnlyList<JobInterviewee> attendees)
    {
        if (attendees.Count == 0)
            return "<p style='font-size:0.82rem;color:#6b7280'>No candidates are booked in yet.</p>";

        var sb = new StringBuilder("<table style='width:100%;border-collapse:collapse;font-size:0.82rem'>\n");
        foreach (var a in attendees)
        {
            var slot = a is { SlotStartTime: { } s, SlotEndTime: { } e }
                ? $"{s:hh\\:mm} – {e:hh\\:mm}"
                : "During the session";
            sb.Append("<tr><td style='padding:0.25rem 0.4rem;border-bottom:1px solid #f3f4f6;width:30%'>")
              .Append(Encode(slot)).Append("</td>")
              .Append("<td style='padding:0.25rem 0.4rem;border-bottom:1px solid #f3f4f6'>")
              .Append(Encode(a.JobApplication?.JobCandidate?.FullName ?? "Candidate")).Append("</td>")
              .Append("<td style='padding:0.25rem 0.4rem;border-bottom:1px solid #f3f4f6;color:#6b7280;width:25%'>")
              .Append(Encode(a.JobApplication?.ApplicationNumber ?? string.Empty)).Append("</td></tr>\n");
        }
        return sb.Append("</table>\n").ToString();
    }

    private static string BuildRecommendationBoxes()
    {
        var sb = new StringBuilder();
        foreach (JobInterviewRecommendation r in Enum.GetValues<JobInterviewRecommendation>())
        {
            sb.Append("<span style='display:inline-flex;align-items:center;gap:0.35rem;border:1.5px solid #374151;")
              .Append("border-radius:4px;padding:0.2rem 0.5rem;font-size:0.78rem'>")
              .Append("<span style='width:12px;height:12px;border:1.5px solid #374151;border-radius:2px'></span>")
              .Append(Encode(Prettify(r.ToString()))).Append("</span>");
        }
        return sb.ToString();
    }

    private static string BuildCommentLines(int count)
    {
        var sb = new StringBuilder("<div style='margin-top:0.3rem'>");
        for (var i = 0; i < count; i++)
            sb.Append("<div style='border-bottom:1px solid #9ca3af;height:15px;margin-top:5px'></div>");
        return sb.Append("</div>").ToString();
    }

    /// <summary>
    /// The candidate's current role and experience, so the panel has context the original omitted.
    /// </summary>
    private static string? BuildCandidateContext(JobInterviewee? attendee)
    {
        var candidate = attendee?.JobApplication?.JobCandidate;
        if (candidate is null) return null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(candidate.CurrentJobTitle))
        {
            parts.Add(string.IsNullOrWhiteSpace(candidate.CurrentEmployer)
                ? $"Currently {candidate.CurrentJobTitle}"
                : $"Currently {candidate.CurrentJobTitle} at {candidate.CurrentEmployer}");
        }

        var years = attendee?.JobApplication?.YearsOfExperience ?? candidate.TotalYearsExperience;
        if (years is > 0) parts.Add($"{years} years' experience");

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    // ── shared helpers ──────────────────────────────────────────────────────────

    private sealed record PanelMember(Guid Id, string? Name, string? Role);

    private static List<PanelMember> BuildPanelList(
        JobInterview interview, IReadOnlyList<JobInterviewExternalPanelist> externals)
    {
        var members = (interview.Panelists ?? new List<JobInterviewPanelist>())
            .Where(p => !p.IsDeleted)
            .Select(p => new PanelMember(
                p.Id,
                $"{p.Employee?.FirstName} {p.Employee?.LastName}".Trim(),
                Prettify(p.Role.ToString())))
            .ToList();

        // Externals carry their organisation, because "Ama Darko" on a sheet a candidate may later
        // see is less useful than "Ama Darko (Institute of Chartered Accountants)".
        members.AddRange(externals.Select(x => new PanelMember(
            x.Id,
            $"{x.ExternalAssociate?.FirstName} {x.ExternalAssociate?.LastName}".Trim(),
            string.IsNullOrWhiteSpace(x.ExternalAssociate?.CompanyName)
                ? Prettify(x.Role.ToString())
                : $"{Prettify(x.Role.ToString())} · {x.ExternalAssociate!.CompanyName}")));

        return members.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList();
    }

    private static List<JobInterviewee> SelectAttendees(JobInterview interview, IReadOnlyList<Guid>? ids)
    {
        var all = (interview.Interviewees ?? new List<JobInterviewee>())
            .Where(i => !i.IsDeleted)
            // Slotted candidates first and in time order — the sheets then come off the printer in
            // the order the panel will see people, which is the only order that helps in the room.
            .OrderBy(i => i.SlotStartTime ?? TimeSpan.MaxValue)
            .ThenBy(i => i.CreatedAt)
            .ToList();

        return ids is { Count: > 0 }
            ? all.Where(i => ids.Contains(i.Id)).ToList()
            : all;
    }

    /// <summary>Concatenates rendered sheets with a page break between, never after the last.</summary>
    private static string Join(IReadOnlyList<string> sheets)
    {
        var real = sheets.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (real.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < real.Count; i++)
        {
            sb.Append("<section class='interview-paper-sheet'");
            if (i < real.Count - 1) sb.Append(" style='page-break-after:always'");
            sb.Append('>').Append(real[i]).Append("</section>\n");
        }
        return sb.ToString();
    }

    private static int CountSheets(string html) =>
        string.IsNullOrEmpty(html) ? 0 : html.Split("interview-paper-sheet").Length - 1;

    private static string Th(string label, string style) =>
        $"<th style='text-align:left;font-weight:600;font-size:0.68rem;text-transform:uppercase;"
        + $"letter-spacing:0.04em;color:#6b7280;padding:0.25rem 0.4rem;border-bottom:1px solid #d1d5db;"
        + $"background:#f9fafb;{style}'>{Encode(label)}</th>";

    private static string Td(string value, string style) =>
        $"<td style='padding:0.35rem 0.4rem;border-bottom:1px solid #f3f4f6;{style}'>{Encode(value)}</td>";

    /// <summary>"StrongNoHire" becomes "Strong No Hire". Enum members are not printable English.</summary>
    private static string Prettify(string pascal) =>
        string.IsNullOrWhiteSpace(pascal)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(pascal, "(?<!^)([A-Z])", " $1");

    /// <summary>Minimal HTML encoding — avoids a System.Web dependency in Core, as the renderer does.</summary>
    private static string Encode(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
