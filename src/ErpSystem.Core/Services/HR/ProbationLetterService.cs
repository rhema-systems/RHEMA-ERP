using System.Globalization;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Probation;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Renders the FR-HR-032 probation confirmation letter through the HR-editable
/// "ProbationConfirmationLetter" template, on the same pattern as <see cref="OfferLetterService"/>.
/// </summary>
/// <remarks>
/// <para>The FRD states the chain as <i>system (month 5) → head confirms → <b>HR issues the
/// confirmation letter</b></i>, so the letter is a consequence of confirmation, not a way of
/// causing it: it can only be produced for a probation that has actually been confirmed.</para>
///
/// <para>The letter is generated on demand rather than stored. Nothing about it is derived — every
/// figure in it is read back from the probation, the employee and their position at the moment of
/// asking — so a stored copy could only ever go stale against the record it describes.</para>
/// </remarks>
public sealed class ProbationLetterService : IProbationLetterService
{
    private readonly IProbationPeriodRepository _probationRepository;
    private readonly IProbationReviewRepository _reviewRepository;
    private readonly IProbationExtensionRepository _extensionRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly ILogger<ProbationLetterService> _logger;

    public ProbationLetterService(
        IProbationPeriodRepository probationRepository,
        IProbationReviewRepository reviewRepository,
        IProbationExtensionRepository extensionRepository,
        IEmployeeRepository employeeRepository,
        ICurrentUserProvider currentUserProvider,
        ITemplatedEmailService templatedEmail,
        ICompanyProfileProvider companyProfile,
        ILogger<ProbationLetterService> logger)
    {
        _probationRepository = probationRepository;
        _reviewRepository = reviewRepository;
        _extensionRepository = extensionRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _templatedEmail = templatedEmail;
        _companyProfile = companyProfile;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. This service scopes every read to the authenticated tenant.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<ProbationConfirmationLetterDto> GenerateConfirmationLetterAsync(
        Guid probationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var probation = await _probationRepository.GetByIdWithDetailsAsync(probationId);
        if (probation == null || probation.TenantId != tenantId)
            throw ProbationWorkflowException.NotFound($"Probation record '{probationId}' was not found.");

        // FR-HR-032: HR issues the letter AFTER confirmation. Producing one for a probation that is
        // still running would be a letter asserting something that has not happened.
        if (probation.Status != ProbationStatus.Completed)
            throw ProbationWorkflowException.InvalidState(
                probation.Status == ProbationStatus.Active
                    ? "This probation has not been confirmed yet, so there is no confirmation to issue a letter for."
                    : $"This probation was {probation.Status}; a confirmation letter can only be issued for a confirmed probation.");

        var employee = await _employeeRepository.GetByIdWithDetailsAsync(probation.EmployeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw ProbationWorkflowException.NotFound($"Employee '{probation.EmployeeId}' was not found.");

        var company = await _companyProfile.GetAsync(cancellationToken);
        var extensions = (await _extensionRepository.GetByProbationIdAsync(probationId))
            .Where(e => e.TenantId == tenantId).ToList();
        var reviews = (await _reviewRepository.GetByProbationPeriodIdAsync(probationId))
            .Where(r => r.TenantId == tenantId).ToList();

        var finalReview = reviews
            .Where(r => r.Recommendation != null)
            .OrderByDescending(r => r.ActualDate ?? r.ScheduledDate)
            .FirstOrDefault();

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = string.IsNullOrWhiteSpace(company.LegalName) ? "Our Company" : company.LegalName,
            ["CompanyAddress"] = NullIfBlank(company.RegisteredAddress),
            ["CompanyLogoUrl"] = NullIfBlank(company.LogoUrl),
            ["CompanyFooter"] = NullIfBlank(company.DocumentFooterText),
            ["LetterDate"] = DateTime.UtcNow.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),

            ["EmployeeName"] = employee.FullName,
            ["EmployeeNumber"] = employee.EmployeeNumber,
            ["PositionTitle"] = employee.Position?.Title,
            ["DepartmentName"] = NullIfBlank(employee.Department?.Name ?? employee.OrganizationUnit?.Name),
            ["StaffLevel"] = employee.Position?.StaffLevel?.Name,

            ["ProbationStartDate"] = LongDate(probation.StartDate),
            ["ProbationEndDate"] = LongDate(probation.CurrentEndDate),
            ["DurationMonths"] = probation.DurationMonths.ToString(CultureInfo.InvariantCulture),
            // Only rendered when it happened — the template guards on it, so a straight-through
            // probation does not get a sentence about extensions that reads as an accusation.
            ["ExtensionText"] = extensions.Count switch
            {
                0 => null,
                1 => $"having been extended once to {LongDate(probation.CurrentEndDate)}",
                _ => $"having been extended {extensions.Count} times to {LongDate(probation.CurrentEndDate)}",
            },
            ["ConfirmationDate"] = employee.ConfirmationDate is { } confirmed
                ? LongDate(confirmed)
                : LongDate(probation.CurrentEndDate),
            ["ReviewSummary"] = finalReview is null
                ? null
                : $"Your final probation review, held on {LongDate(finalReview.ActualDate ?? finalReview.ScheduledDate)}, "
                  + $"recorded a recommendation to {Prettify(finalReview.Recommendation!.Value.ToString()).ToLowerInvariant()}.",

            ["SignatoryName"] = NullIfBlank(company.DefaultSignatoryName),
            ["SignatoryTitle"] = string.IsNullOrWhiteSpace(company.DefaultSignatoryTitle)
                ? "Head of Human Resources"
                : company.DefaultSignatoryTitle,
        };

        var rendered = await _templatedEmail.RenderAsync(
            ProbationEmailCatalog.Module, ProbationEmailCatalog.Events.ConfirmationLetter, tokens, cancellationToken);

        if (rendered is null)
            throw new InvalidOperationException("No probation confirmation-letter template or built-in default is available.");

        _logger.LogInformation(
            "Confirmation letter rendered for probation {ProbationId} (employee {EmployeeId})",
            probationId, employee.Id);

        return new ProbationConfirmationLetterDto
        {
            ProbationId = probation.Id,
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            EmployeeNumber = employee.EmployeeNumber,
            PositionTitle = employee.Position?.Title ?? string.Empty,
            ConfirmationDate = employee.ConfirmationDate ?? probation.CurrentEndDate,
            Subject = rendered.Subject,
            HtmlBody = rendered.HtmlBody,
        };
    }

    private static string LongDate(DateOnly value)
        => value.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"ContinueMonitoring" -> "Continue Monitoring".</summary>
    private static string Prettify(string value)
        => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
