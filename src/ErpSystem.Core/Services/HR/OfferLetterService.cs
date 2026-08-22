using System.Globalization;
using System.Text;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
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
/// Assembles the enriched offer-letter token set from a <see cref="JobOffer"/> (position/grade,
/// itemised salary breakdown from the position's pay components, benefits, job-description summary
/// and key duties, bargaining-unit status, pre-employment conditions, letterhead + signatory) and
/// renders it through the HR-editable "OfferLetter" template via <see cref="ITemplatedEmailService"/>.
/// </summary>
public sealed class OfferLetterService : IOfferLetterService
{
    private readonly IJobOfferRepository _offerRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ILogger<OfferLetterService> _logger;

    public OfferLetterService(
        IJobOfferRepository offerRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ITemplatedEmailService templatedEmail,
        ICompanyProfileProvider companyProfile,
        ILogger<OfferLetterService> logger)
    {
        _offerRepository = offerRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _templatedEmail = templatedEmail;
        _companyProfile = companyProfile;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<OfferLetterDto> GenerateAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetWithFullDetailsAsync(offerId);
        if (offer == null || offer.TenantId != GetTenantId())
            throw new ArgumentException($"Job offer with ID '{offerId}' not found.");

        var candidateName = offer.Application?.JobCandidate?.FullName ?? "Candidate";
        var currency = string.IsNullOrWhiteSpace(offer.CurrencyCode) ? "GHS" : offer.CurrencyCode!.Trim();

        // Company (legal-employer) details for the letterhead / signature / footer. The provider
        // returns the saved CompanyProfile or Tenant/config-resolved defaults.
        var company = await _companyProfile.GetAsync(cancellationToken);

        // Enrichment sources not carried on the offer itself.
        var jobDescription = await LoadJobDescriptionAsync(offer.PositionId, offer.TenantId, cancellationToken);
        var payComponents = await LoadAllowanceComponentsAsync(offer.PositionId, offer.TenantId, cancellationToken);
        var preCheck = offer.IsConditional
            ? await LoadPreEmploymentCheckAsync(offer.Id, offer.TenantId, cancellationToken)
            : null;

        var (salaryTable, baseSalaryLine, grossSalaryLine) = BuildSalaryBreakdown(offer, payComponents, currency);

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // Letterhead (from the tenant CompanyProfile, resolved with Tenant/config fallback).
            ["CompanyName"]    = string.IsNullOrWhiteSpace(company.LegalName) ? "Our Company" : company.LegalName,
            ["CompanyAddress"] = ComposeCompanyAddress(company),
            ["CompanyLogoUrl"] = company.LogoUrl,
            ["CompanyFooter"]  = NullIfBlank(company.DocumentFooterText),
            ["LetterDate"]     = (offer.OfferDate ?? DateTime.UtcNow).ToString("d MMMM yyyy", CultureInfo.InvariantCulture),

            ["CandidateName"]    = candidateName,
            ["CandidateAddress"] = offer.Application?.JobCandidate?.PostalAddress,
            ["OfferNumber"]      = offer.OfferNumber,

            // Position & terms.
            ["PositionTitle"]  = offer.PositionTitle,
            ["DepartmentName"] = NullIfBlank(offer.DepartmentName),
            ["GradeTitle"]     = NullIfBlank(offer.GradeTitle),
            ["StaffLevel"]     = jobDescription?.StaffLevel?.Name,
            ["ReportsToTitle"] = NullIfBlank(offer.ReportsToTitle),
            ["EmploymentType"] = Prettify(offer.EmploymentType.ToString()),
            ["WorkMode"]       = Prettify(offer.WorkMode.ToString()),
            ["LocationName"]   = offer.Location?.Name,
            ["StartDate"]      = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture),
            ["ProbationText"]  = offer.ProbationPeriodMonths is > 0 ? $"{offer.ProbationPeriodMonths} month(s)" : null,
            ["NoticeText"]     = offer.NoticePeriodMonths is > 0 ? $"{offer.NoticePeriodMonths} month(s)" : null,
            ["WeeklyHours"]    = offer.WeeklyHours?.ToString("0.#", CultureInfo.InvariantCulture),
            ["AnnualLeaveDays"] = offer.AnnualLeaveDays?.ToString(CultureInfo.InvariantCulture),

            // Bargaining unit.
            ["IsBargainingUnit"] = jobDescription?.IsBargainingUnitRole == true ? "true" : null,
            ["UnionName"]        = jobDescription?.Union?.Name,

            // Role summary + duties.
            ["JobSummary"]         = NullIfBlank(jobDescription?.JobSummary),
            ["DutiesList"]         = BuildDutiesList(jobDescription),
            ["EssentialFunctions"] = NullIfBlank(jobDescription?.EssentialFunctionsSummary),

            // Remuneration.
            ["SalaryBreakdownTable"] = salaryTable,
            ["BaseSalaryLine"]       = baseSalaryLine,
            ["GrossSalaryLine"]      = grossSalaryLine,
            ["BonusTerms"]           = NullIfBlank(offer.BonusTerms),
            ["CommissionStructure"]  = NullIfBlank(offer.CommissionStructure),

            // Benefits.
            ["BenefitsList"] = BuildBenefitsList(offer, currency),

            // Clauses.
            ["NdaRequired"]    = offer.NdaRequired ? "true" : null,
            ["IsConditional"]  = offer.IsConditional ? "true" : null,
            ["ConditionsList"] = offer.IsConditional ? BuildConditionsList(preCheck) : null,
            ["AdditionalTerms"] = NullIfBlank(offer.AdditionalTerms),

            // Acceptance & signature.
            ["ExpiryDate"]             = offer.ExpiryDate?.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture),
            ["AcceptanceInstructions"] = NullIfBlank(company.OfferAcceptanceInstructions)
                ?? "To accept this offer, please sign and return one copy of this letter, or record your acceptance through the candidate portal.",
            ["SignatoryName"]  = company.DefaultSignatoryName,
            ["SignatoryTitle"] = string.IsNullOrWhiteSpace(company.DefaultSignatoryTitle) ? "Head of Human Resources" : company.DefaultSignatoryTitle,

            // `SignatureImageUrl` and `CompanySealImageUrl` are stored on CompanyProfile and were
            // editable in principle, but neither ever became a token — so no template could reference
            // them however it was written, and the only image any letter could render was the logo.
            // Emitting them here is what makes the two fields mean something; a template that does not
            // use them is unaffected, because an unused token is simply not substituted.
            ["SignatureImageUrl"]   = NullIfBlank(company.SignatureImageUrl),
            ["CompanySealImageUrl"] = NullIfBlank(company.CompanySealImageUrl),
        };

        var rendered = await _templatedEmail.RenderAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.OfferLetter, tokens, cancellationToken);

        if (rendered is null)
            throw new InvalidOperationException("No offer-letter template or built-in default is available.");

        return new OfferLetterDto
        {
            OfferId       = offer.Id,
            OfferNumber   = offer.OfferNumber,
            CandidateName = candidateName,
            PositionTitle = offer.PositionTitle,
            Subject       = rendered.Subject,
            HtmlBody      = rendered.HtmlBody,
        };
    }

    // ── Enrichment loaders ──────────────────────────────────────────────────────

    private async Task<JobDescription?> LoadJobDescriptionAsync(Guid positionId, Guid tenantId, CancellationToken ct)
    {
        return await _unitOfWork.Repository<JobDescription>().GetQueryable()
            .Include(j => j.DutyItems)
            .Include(j => j.StaffLevel)
            .Include(j => j.Union)
            .Where(j => j.TenantId == tenantId && j.PositionId == positionId && !j.IsDeleted && j.SupersededByVersionId == null)
            .OrderByDescending(j => j.Status == JobDescriptionStatus.Active)
            .ThenByDescending(j => j.Status == JobDescriptionStatus.Approved)
            .ThenByDescending(j => j.EffectiveDate)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<PositionPayComponent>> LoadAllowanceComponentsAsync(Guid positionId, Guid tenantId, CancellationToken ct)
    {
        return await _unitOfWork.Repository<PositionPayComponent>().GetQueryable()
            .Include(p => p.PayComponent)
            .Where(p => p.TenantId == tenantId && p.PositionId == positionId && p.IsActive && !p.IsDeleted
                     && p.PayComponent != null
                     && p.PayComponent.IsActive
                     && p.PayComponent.ComponentType == PayComponentType.Allowance
                     && p.PayComponent.AffectsGrossPay)
            .ToListAsync(ct);
    }

    private async Task<PreEmploymentCheck?> LoadPreEmploymentCheckAsync(Guid offerId, Guid tenantId, CancellationToken ct)
    {
        return await _unitOfWork.Repository<PreEmploymentCheck>().GetQueryable()
            .Include(p => p.Items)
            .Where(p => p.TenantId == tenantId && p.JobOfferId == offerId && !p.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    // ── HTML fragment builders (values are HTML-encoded before injection) ────────

    private (string Table, string? BaseLine, string? GrossLine) BuildSalaryBreakdown(
        JobOffer offer, IReadOnlyList<PositionPayComponent> components, string currency)
    {
        var basic = offer.BaseSalary ?? 0m;

        // Nothing quantified — remuneration stated as to-be-confirmed.
        if (basic <= 0 && components.Count == 0)
            return ("<p style='font-size:13px'>Your remuneration will be confirmed separately.</p>", null, null);

        var sb = new StringBuilder();
        sb.Append("<table style='width:100%;border-collapse:collapse;font-size:13px'>");
        sb.Append(SalaryRow("Basic salary", FormatMoney(basic, currency), header: true));

        var gross = basic;
        foreach (var pc in components.OrderBy(c => c.PayComponent.Name, StringComparer.OrdinalIgnoreCase))
        {
            var comp = pc.PayComponent;
            var configured = pc.Amount ?? comp.DefaultAmount ?? 0m;
            var amount = comp.CalculationBasis == PayComponentCalculationBasis.PercentageOfBasic
                ? Math.Round(basic * (configured / 100m), 2)
                : configured;

            if (amount <= 0) continue;

            gross += amount;
            sb.Append(SalaryRow(comp.Name, FormatMoney(amount, currency), header: false));
        }

        var hasAllowances = gross != basic;
        if (hasAllowances)
            sb.Append(SalaryRow("Gross", FormatMoney(gross, currency), header: true, highlight: true));

        sb.Append("</table>");
        sb.Append("<p style='font-size:11px;color:#9ca3af;margin:0.35rem 0 0'>Amounts are indicative and stated per annum unless otherwise agreed.</p>");

        var baseLine = basic > 0 ? $"{FormatMoney(basic, currency)} per annum" : null;
        var grossLine = hasAllowances ? $"{FormatMoney(gross, currency)} per annum" : null;
        return (sb.ToString(), baseLine, grossLine);
    }

    private static string SalaryRow(string label, string value, bool header, bool highlight = false)
    {
        var bg = highlight ? "#eef2ff" : (header ? "#f9fafb" : "#ffffff");
        var weight = header ? "700" : "400";
        return $"<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:{bg};font-weight:600'>{HtmlEncode(label)}</td>" +
               $"<td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:{bg};text-align:right;font-weight:{weight}'>{HtmlEncode(value)}</td></tr>";
    }

    private static string? BuildDutiesList(JobDescription? jd)
    {
        var duties = jd?.DutyItems?.Where(d => !d.IsDeleted).OrderBy(d => d.SequenceNumber).ToList();
        if (duties is null || duties.Count == 0) return null;

        var sb = new StringBuilder("<ul style='margin:0.25rem 0 0.75rem;padding-left:1.25rem;font-size:13px'>");
        foreach (var d in duties)
            sb.Append($"<li>{HtmlEncode(d.DutyStatement)}</li>");
        sb.Append("</ul>");
        return sb.ToString();
    }

    private static string? BuildBenefitsList(JobOffer offer, string currency)
    {
        var benefits = offer.Benefits?.Where(b => !b.IsDeleted).OrderBy(b => b.DisplayOrder).ThenBy(b => b.BenefitName).ToList();
        if (benefits is null || benefits.Count == 0) return null;

        var sb = new StringBuilder("<ul style='margin:0.25rem 0 0.75rem;padding-left:1.25rem;font-size:13px'>");
        foreach (var b in benefits)
        {
            var line = HtmlEncode(b.BenefitName);
            if (!string.IsNullOrWhiteSpace(b.Description))
                line += " &mdash; " + HtmlEncode(b.Description!);
            if (b.IsMonetary && b.MonetaryValue.HasValue)
                line += $" ({HtmlEncode(FormatMoney(b.MonetaryValue.Value, b.CurrencyCode ?? currency))})";
            sb.Append($"<li>{line}</li>");
        }
        sb.Append("</ul>");
        return sb.ToString();
    }

    private static string BuildConditionsList(PreEmploymentCheck? preCheck)
    {
        var items = preCheck?.Items?.Where(i => !i.IsDeleted).ToList();

        var labels = (items is { Count: > 0 })
            ? items.Select(i => !string.IsNullOrWhiteSpace(i.Name) ? i.Name! : Prettify(i.CheckType.ToString()))
            : new[]
            {
                "Satisfactory employment references",
                "Verification of stated qualifications",
                "Confirmation of the right to work",
                "Satisfactory background / criminal-record check",
                "Medical fitness assessment",
            };

        var sb = new StringBuilder("<ul style='margin:0.25rem 0 0.75rem;padding-left:1.25rem;font-size:13px'>");
        foreach (var label in labels)
            sb.Append($"<li>{HtmlEncode(label)}</li>");
        sb.Append("</ul>");
        return sb.ToString();
    }

    // ── Small helpers ───────────────────────────────────────────────────────────

    /// <summary>Registered address for the letterhead; falls back to composing from city/region/country.</summary>
    private static string? ComposeCompanyAddress(CompanyProfile company)
    {
        if (!string.IsNullOrWhiteSpace(company.RegisteredAddress))
            return company.RegisteredAddress.Trim();

        var parts = new[] { company.City, company.Region, company.Country?.Name }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim());
        var composed = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(composed) ? null : composed;
    }

    private static string FormatMoney(decimal amount, string currency) =>
        $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Inserts spaces before internal capitals so enum names read as words (e.g. "PartTime" → "Part Time").</summary>
    private static string Prettify(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return pascal;
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(pascal[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string HtmlEncode(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
    }
}
