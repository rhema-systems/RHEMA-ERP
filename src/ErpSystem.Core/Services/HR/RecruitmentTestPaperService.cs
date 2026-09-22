using System.Text;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Builds the printed recruitment test — the question paper or the marking key — and renders it
/// through the HR-editable <c>RecruitmentTests</c> templates. Round 4, lane E6.
/// </summary>
/// <remarks>
/// <para><b>Structure here, language in the template.</b> The question block and the key are facts
/// about the paper, so this service composes them. What the page SAYS around them — the standing
/// instructions to candidates, the marking rules on the key — belongs to the employer and lives in
/// <see cref="RecruitmentTestPaperCatalog"/>. The split lane F's <c>InterviewPaperService</c> makes.</para>
///
/// <para>⚠ <b>Every value going into a raw block is HTML-encoded here.</b> The question block and the
/// key are handed to the template raw (<c>{{{…}}}</c>), so the renderer will not encode them; a
/// question typed as <c>&lt;script&gt;</c> must print as those characters, not run.</para>
/// </remarks>
public sealed class RecruitmentTestPaperService : IRecruitmentTestPaperService
{
    private readonly IRecruitmentTestRepository _testRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<RecruitmentTestPaperService> _logger;

    public RecruitmentTestPaperService(
        IRecruitmentTestRepository testRepository,
        IUnitOfWork unitOfWork,
        ITemplatedEmailService templatedEmail,
        ICompanyProfileProvider companyProfile,
        ICurrentUserProvider currentUserProvider,
        ILogger<RecruitmentTestPaperService> logger)
    {
        _testRepository = testRepository;
        _unitOfWork = unitOfWork;
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
    public async Task<RecruitmentTestPaperDto> GenerateAsync(
        Guid testId,
        RecruitmentTestPaperVariant variant,
        Guid? assignmentId,
        IReadOnlyList<Guid>? applicationIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var paper = await _testRepository.GetWithFullPaperAsync(testId, cancellationToken);
        if (paper is null || paper.TenantId != tenantId)
            throw new ArgumentException($"Recruitment test '{testId}' not found.");

        // ⚠ Authoring order, ALWAYS — never the paper's online shuffle. One key has to fit every
        // script in the pile; see RecruitmentTestPaperVariant.QuestionPaper.
        var questions = paper.Questions
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.DisplayOrder).ThenBy(q => q.CreatedAt)
            .ToList();

        if (questions.Count == 0)
            throw new InvalidOperationException(
                $"'{paper.Name}' has no questions, so there is nothing to print.");

        var company = await _companyProfile.GetAsync(cancellationToken);
        var total = questions.Sum(q => q.Points);
        var written = questions.Where(q => q.QuestionType == RecruitmentQuestionType.FreeText).Sum(q => q.Points);

        var common = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = string.IsNullOrWhiteSpace(company.LegalName) ? "Our Company" : company.LegalName,
            ["CompanyAddress"] = company.RegisteredAddress,
            ["CompanyLogoUrl"] = company.LogoUrl,
            ["TestName"] = paper.Name,
            ["TestCode"] = paper.TestCode,
            ["DurationText"] = paper.DurationMinutes is { } minutes ? $"{minutes} minutes" : "No time limit",
            ["QuestionCount"] = questions.Count.ToString(),
            ["TotalPoints"] = Marks(total),
            ["Instructions"] = paper.Instructions,
        };

        string html;

        if (variant == RecruitmentTestPaperVariant.MarkingKey)
        {
            var tokens = new Dictionary<string, string?>(common, StringComparer.OrdinalIgnoreCase)
            {
                ["AutoMarkablePoints"] = Marks(total - written),
                ["WrittenPoints"] = Marks(written),
                ["PassMarkText"] = paper.PassMarkPercent is { } pass ? $"{pass:0.##}%" : "none set",
                ["KeyBlock"] = BuildKeyBlock(questions),
            };

            html = Join(new[] { await RenderAsync(RecruitmentTestPaperCatalog.Events.MarkingKey, tokens, cancellationToken) });
        }
        else
        {
            var block = BuildQuestionBlock(questions);
            var candidates = await ResolveCandidatesAsync(paper, assignmentId, applicationIds, tenantId, cancellationToken);
            var sheets = new List<string>();

            if (candidates is null)
            {
                // A blank paper: the template draws lines where the name and reference go.
                var tokens = new Dictionary<string, string?>(common, StringComparer.OrdinalIgnoreCase)
                {
                    ["QuestionBlock"] = block,
                };
                sheets.Add(await RenderAsync(RecruitmentTestPaperCatalog.Events.QuestionPaper, tokens, cancellationToken));
            }
            else
            {
                foreach (var candidate in candidates)
                {
                    var tokens = new Dictionary<string, string?>(common, StringComparer.OrdinalIgnoreCase)
                    {
                        ["QuestionBlock"] = block,
                        ["CandidateName"] = candidate.JobCandidate?.FullName,
                        ["ApplicationNumber"] = candidate.ApplicationNumber,
                        ["JobTitle"] = candidate.JobVacancy?.JobTitle,
                    };
                    sheets.Add(await RenderAsync(RecruitmentTestPaperCatalog.Events.QuestionPaper, tokens, cancellationToken));
                }
            }

            html = Join(sheets);
        }

        _logger.LogInformation("Printed {Variant} for {Code}: {Sheets} sheet(s).",
            variant, paper.TestCode, CountSheets(html));

        return new RecruitmentTestPaperDto
        {
            TestId = paper.Id,
            TestCode = paper.TestCode,
            TestName = paper.Name,
            Variant = variant,
            SheetCount = CountSheets(html),
            HtmlBody = html,
        };
    }

    /// <summary>
    /// The candidates to print named papers for, alphabetically — or null for one blank paper.
    /// </summary>
    /// <remarks>
    /// Alphabetical because the pile comes off the printer in that order and is handed out at a
    /// sign-in desk, where it is looked up by name.
    /// </remarks>
    private async Task<List<JobApplication>?> ResolveCandidatesAsync(
        RecruitmentTest paper,
        Guid? assignmentId,
        IReadOnlyList<Guid>? applicationIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (assignmentId is not { } id || id == Guid.Empty) return null;

        var assignment = await _unitOfWork.Repository<RecruitmentTestAssignment>().GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Test assignment '{id}' not found.");

        if (assignment.RecruitmentTestId != paper.Id)
            throw new InvalidOperationException(
                "That assignment is for a different paper, so its candidates cannot be printed this one.");

        var query = _unitOfWork.Repository<JobApplication>().GetQueryable()
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription)
            .Where(a => a.TenantId == tenantId);

        query = assignment.JobApplicationId is { } single
            ? query.Where(a => a.Id == single)
            : query.Where(a => a.JobVacancyId == assignment.JobVacancyId);

        var reached = (await query.ToListAsync(cancellationToken))
            .Where(a => RecruitmentTestReach.Reaches(assignment, a))
            .ToList();

        if (applicationIds is { Count: > 0 })
        {
            var unknown = applicationIds.Where(x => reached.All(a => a.Id != x)).ToList();
            if (unknown.Count > 0)
                throw new InvalidOperationException(
                    $"{unknown.Count} of the candidates asked for are not ones this assignment reaches — "
                    + "withdrawn, rejected, or on another vacancy — so no paper is printed for them. "
                    + "Nothing was printed; ask again without them.");

            reached = reached.Where(a => applicationIds.Contains(a.Id)).ToList();
        }

        if (reached.Count == 0)
            throw new InvalidOperationException(
                "There is nobody to print for. A vacancy-wide test reaches applications that are still "
                + "live — not the withdrawn, rejected or already-hired ones.");

        return reached
            .OrderBy(a => a.JobCandidate?.LastName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.JobCandidate?.FirstName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── The question block — what the candidate writes on ────────────────────────

    private static string BuildQuestionBlock(IReadOnlyList<RecruitmentTestQuestion> questions)
    {
        var sb = new StringBuilder();
        Guid? section = null;
        var number = 0;

        foreach (var q in questions)
        {
            number++;

            // A section heading where the section changes — in authoring order, so a paper written as
            // "numerical, then verbal" prints that way.
            if (q.RecruitmentTestSectionId != section)
            {
                section = q.RecruitmentTestSectionId;
                if (q.Section is { IsDeleted: false } s)
                    sb.Append("<h2 style='font-size:0.95rem;margin:1.25rem 0 0.25rem;border-bottom:1px solid #d1d5db;padding-bottom:0.15rem'>")
                      .Append(Encode(s.Name)).Append("</h2>\n");
            }

            sb.Append("<div class='interview-paper-keep' style='margin:0.85rem 0 0.35rem'>\n")
              .Append("  <div style='display:flex;justify-content:space-between;gap:1rem'>")
              .Append("<div><strong>").Append(number).Append(".</strong> ").Append(Encode(q.QuestionText)).Append("</div>")
              .Append("<div style='white-space:nowrap;font-size:0.75rem;color:#6b7280'>[")
              .Append(Marks(q.Points)).Append(q.Points == 1 ? " mark" : " marks").Append("]</div></div>\n");

            var hint = q.QuestionType switch
            {
                RecruitmentQuestionType.SingleChoice or RecruitmentQuestionType.TrueFalse => "Tick one box.",
                RecruitmentQuestionType.MultiSelect => "Tick every box that applies.",
                RecruitmentQuestionType.Numeric => "Write your answer as a number.",
                _ => "Write your answer in the space below.",
            };
            sb.Append("  <div style='font-size:0.72rem;color:#6b7280;margin:0.1rem 0 0.25rem 1.25rem'>")
              .Append(hint).Append("</div>\n");

            switch (q.QuestionType)
            {
                case RecruitmentQuestionType.Numeric:
                    sb.Append("  <div style='margin:0.35rem 0 0 1.25rem'>Answer: ")
                      .Append("<span style='display:inline-block;border-bottom:1px solid #111827;width:11rem'>&nbsp;</span></div>\n");
                    break;

                case RecruitmentQuestionType.FreeText:
                    // Room to write, in proportion to what the answer is worth — a one-mark question
                    // needs a line, a ten-mark one needs a half page.
                    var lines = Math.Clamp(3 + (int)Math.Ceiling(q.Points), 4, 14);
                    for (var i = 0; i < lines; i++)
                        sb.Append("  <div style='border-bottom:1px solid #9ca3af;height:1.55rem;margin-left:1.25rem'></div>\n");
                    break;

                default:
                    foreach (var option in q.Options.Where(o => !o.IsDeleted).OrderBy(o => o.DisplayOrder).ThenBy(o => o.CreatedAt))
                        sb.Append("  <div style='margin:0.2rem 0 0 1.25rem'>&#9744;&nbsp; ")
                          .Append(Encode(option.OptionText)).Append("</div>\n");
                    break;
            }

            sb.Append("</div>\n");
        }

        return sb.ToString();
    }

    // ── The key — what the marker marks from ─────────────────────────────────────

    private static string BuildKeyBlock(IReadOnlyList<RecruitmentTestQuestion> questions)
    {
        var sb = new StringBuilder();
        sb.Append("<table style='width:100%;border-collapse:collapse;font-size:0.82rem;margin-top:1rem'>\n<thead><tr>")
          .Append(Th("#", "width:2rem"))
          .Append(Th("Question", ""))
          .Append(Th("Correct answer", "width:40%"))
          .Append(Th("Marks", "width:3.5rem;text-align:right"))
          .Append("</tr></thead>\n<tbody>\n");

        var number = 0;
        foreach (var q in questions)
        {
            number++;
            sb.Append("<tr style='vertical-align:top'>")
              .Append(Td(number.ToString(), ""))
              .Append("<td style='padding:0.35rem 0.4rem;border-bottom:1px solid #e5e7eb'>")
              .Append(Encode(q.QuestionText));

            if (q.Section is { IsDeleted: false } s)
                sb.Append("<div style='font-size:0.7rem;color:#9ca3af'>").Append(Encode(s.Name)).Append("</div>");

            if (!string.IsNullOrWhiteSpace(q.Explanation))
                sb.Append("<div style='font-size:0.74rem;color:#4b5563;margin-top:0.2rem'><em>")
                  .Append(Encode(q.Explanation)).Append("</em></div>");

            sb.Append("</td><td style='padding:0.35rem 0.4rem;border-bottom:1px solid #e5e7eb'>")
              .Append(AnswerCell(q))
              .Append("</td>")
              .Append(Td(Marks(q.Points), "text-align:right"))
              .Append("</tr>\n");
        }

        sb.Append("</tbody></table>\n");
        return sb.ToString();
    }

    private static string AnswerCell(RecruitmentTestQuestion q)
    {
        switch (q.QuestionType)
        {
            case RecruitmentQuestionType.Numeric:
                return $"<strong>{Encode(q.ExpectedAnswer)}</strong> "
                     + "<span style='color:#6b7280;font-size:0.72rem'>— compared as a number</span>";

            case RecruitmentQuestionType.FreeText:
                var note = string.IsNullOrWhiteSpace(q.ExpectedAnswer)
                    ? "<span style='color:#6b7280'>No marking note was written for this question.</span>"
                    : Encode(q.ExpectedAnswer);
                return $"<div style='font-size:0.72rem;text-transform:uppercase;letter-spacing:0.04em;color:#6b7280'>"
                     + $"Marked by hand, 0 – {Marks(q.Points)}</div>{note}";

            default:
                var sb = new StringBuilder();
                var options = q.Options.Where(o => !o.IsDeleted).OrderBy(o => o.DisplayOrder).ThenBy(o => o.CreatedAt);
                foreach (var option in options)
                {
                    sb.Append(option.IsCorrect
                        ? "<div><strong>&#10003; " + Encode(option.OptionText) + "</strong></div>"
                        : "<div style='color:#9ca3af'>&#9744; " + Encode(option.OptionText) + "</div>");
                }

                if (q.QuestionType == RecruitmentQuestionType.MultiSelect)
                    sb.Append("<div style='font-size:0.72rem;color:#6b7280;margin-top:0.15rem'>")
                      .Append("Every ticked answer, and none of the others.</div>");

                return sb.ToString();
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private async Task<string> RenderAsync(
        string eventKey, IReadOnlyDictionary<string, string?> tokens, CancellationToken ct)
    {
        var rendered = await _templatedEmail.RenderAsync(RecruitmentTestPaperCatalog.Module, eventKey, tokens, ct);
        if (rendered is null)
            throw new InvalidOperationException(
                $"No '{eventKey}' template is available. The shipped default should always resolve — "
                + "check that the RecruitmentTests catalogue is registered.");
        return rendered.HtmlBody;
    }

    /// <summary>
    /// Concatenates papers with a page break between, never after the last.
    /// </summary>
    /// <remarks>
    /// ⚠ The class is lane F's <c>interview-paper-sheet</c>, reused on purpose: the printed-paper
    /// stylesheet hangs its page breaks and its keep-together rules on it, and a second copy of those
    /// rules under another name is how two print stylesheets drift apart.
    /// </remarks>
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

    private static string Marks(decimal value) => value.ToString("0.##");

    private static string Th(string label, string style) =>
        "<th style='text-align:left;font-weight:600;font-size:0.68rem;text-transform:uppercase;"
        + "letter-spacing:0.04em;color:#6b7280;padding:0.25rem 0.4rem;border-bottom:1px solid #d1d5db;"
        + $"background:#f9fafb;{style}'>{Encode(label)}</th>";

    private static string Td(string text, string style) =>
        $"<td style='padding:0.35rem 0.4rem;border-bottom:1px solid #e5e7eb;{style}'>{Encode(text)}</td>";

    private static string Encode(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                   .Replace("\"", "&quot;").Replace("'", "&#39;");
}
