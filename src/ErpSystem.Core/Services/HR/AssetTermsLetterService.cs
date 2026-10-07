using System.Globalization;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Assets;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Renders the AST-5 responsibility-and-terms document through the HR-editable
/// "AssetResponsibilityTerms" template, on the same pattern as <see cref="ProbationLetterService"/>
/// and <c>OfferLetterService</c>.
/// </summary>
/// <remarks>
/// <para><b>Why this replaced a QuestPDF builder.</b> The first cut of this slice generated a PDF
/// with QuestPDF, following decision D7 — which had cited procurement's <c>AwardLetterService</c> as
/// "how the repo generates documents". That was the wrong exemplar: <b>HR</b> generates letters as
/// self-contained HTML from an editable template plus <see cref="ICompanyProfileProvider"/>, which
/// is both printable (browser print-to-PDF, for the physical signature AST-5 asks for) and editable
/// by the client without a deployment. It also killed two defects the PDF version had: company
/// details read from <c>IConfiguration</c> rather than the per-tenant company profile, and the
/// liability wording hard-coded in C# where nobody but a developer could change it.</para>
///
/// <para>The letter is generated on demand rather than stored. Every value in it is read back from
/// the assignment, the asset and the employee at the moment of asking, so a stored copy could only
/// go stale against the record it describes.</para>
///
/// <para><b>One render, two routes.</b> Downloading and emailing resolve the same template with the
/// same tokens — a printed form and an emailed one that disagree about what somebody is responsible
/// for is worse than having neither.</para>
/// </remarks>
public sealed class AssetTermsLetterService : IAssetTermsLetterService
{
    private readonly IAssetAssignmentRepository _assignmentRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly ITransactionalEmailQueue _emailQueue;
    private readonly ICompanyProfileProvider _companyProfile;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssetTermsLetterService> _logger;

    public AssetTermsLetterService(
        IAssetAssignmentRepository assignmentRepo,
        ICurrentUserService currentUserService,
        ITemplatedEmailService templatedEmail,
        ITransactionalEmailQueue emailQueue,
        ICompanyProfileProvider companyProfile,
        IUnitOfWork unitOfWork,
        ILogger<AssetTermsLetterService> logger)
    {
        _assignmentRepo = assignmentRepo;
        _currentUserService = currentUserService;
        _templatedEmail = templatedEmail;
        _emailQueue = emailQueue;
        _companyProfile = companyProfile;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AssetTermsLetterDto> GenerateAsync(
        Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await RequireReadableAssignmentAsync(assignmentId);
        var (rendered, _) = await RenderAsync(assignment, cancellationToken);

        return new AssetTermsLetterDto
        {
            AssignmentId = assignment.Id,
            AssignmentNumber = assignment.AssignmentNumber,
            EmployeeId = assignment.EmployeeId,
            EmployeeName = FullName(assignment),
            AssetId = assignment.AssetId,
            AssetNumber = assignment.Asset?.AssetNumber ?? string.Empty,
            Subject = rendered.Subject,
            HtmlBody = rendered.HtmlBody,
            TermsDocumentSentAt = assignment.TermsDocumentSentAt,
            TermsDocumentSentTo = assignment.TermsDocumentSentTo,
        };
    }

    public async Task<AssetTermsLetterSendResultDto> EmailAsync(
        Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await RequireReadableAssignmentAsync(assignmentId);

        var address = assignment.Employee?.EmailAddress;
        if (string.IsNullOrWhiteSpace(address))
            throw AssetsWorkflowException.Invalid(
                $"No email address is recorded for {FullName(assignment)}, so the responsibility "
                + "document cannot be sent. Add one to their employee record, or print the document "
                + "and have it signed.");

        var sentById = AssetActor.CallerEmployeeId(_currentUserService)
            ?? throw new UnauthorizedAccessException(
                "Your user account is not linked to an employee record, so it cannot send an asset "
                + "responsibility document.");

        var (rendered, _) = await RenderAsync(assignment, cancellationToken);

        // ⚠ RENDERED through the template service, DELIVERED through the durable outbox — and the
        // split is deliberate.
        //
        // `ITemplatedEmailService.SendAsync` would do both in one call, but it hands straight to
        // SMTP and, by its own contract, "never throws for a send failure — returns false and logs".
        // For a notification that is the right trade. This is not a notification: it is the document
        // stating what an employee is financially liable for, and the next line stamps the record to
        // say they were told. That claim has to rest on something durable, so it goes through the
        // Notifications outbox, where a dispatcher retries it and an administrator can see whether
        // it left. EnqueueAsync throws when the row cannot be written, so a failure here means
        // nothing is stamped — the outcome worse than failing loudly is a register that says an
        // employee was told when they were not.
        await _emailQueue.EnqueueAsync(new TransactionalEmailRequest
        {
            TenantId = assignment.TenantId,
            ToEmail = address,
            Subject = rendered.Subject,
            BodyHtml = rendered.HtmlBody,
            NotificationType = AssetsEmailCatalog.Events.ResponsibilityTerms,
        }, cancellationToken);

        assignment.TermsDocumentSentAt = DateTime.UtcNow;
        assignment.TermsDocumentSentTo = address.Length > 256 ? address[..256] : address;
        assignment.TermsDocumentSentById = sentById;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = _currentUserService.UserId;

        await _assignmentRepo.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Asset responsibility document for assignment {AssignmentNumber} queued to {Address}",
            assignment.AssignmentNumber, address);

        var reread = await _assignmentRepo.GetWithDetailsAsync(assignmentId);
        return new AssetTermsLetterSendResultDto
        {
            AssignmentId = assignment.Id,
            AssignmentNumber = assignment.AssignmentNumber,
            SentTo = address,
            SentAt = assignment.TermsDocumentSentAt.Value,
            SentByName = reread?.TermsDocumentSentBy is { } s ? $"{s.FirstName} {s.LastName}" : string.Empty,
        };
    }

    // ── the record, and who may see it ───────────────────────────────────────────────────────────

    /// <summary>
    /// The assignment, loaded with everything the document names, and gated on its holder.
    /// </summary>
    /// <remarks>
    /// Self-or-HR, the same rule as reading the assignment itself: this document states what one
    /// named employee is responsible for, and it is theirs to print as much as it is HR's to serve.
    /// </remarks>
    private async Task<AssetAssignment> RequireReadableAssignmentAsync(Guid assignmentId)
    {
        var assignment = await _assignmentRepo.GetWithDetailsAsync(assignmentId);
        if (assignment is null || assignment.TenantId != RequireTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset assignment was found with id {assignmentId}.");

        AssetActor.EnsureSelfOrHr(_currentUserService, assignment.EmployeeId,
            "read the responsibility document for an asset assignment");

        return assignment;
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    // ── the tokens the template merges ───────────────────────────────────────────────────────────

    private async Task<(RenderedEmail Rendered, Dictionary<string, string?> Tokens)> RenderAsync(
        AssetAssignment a, CancellationToken cancellationToken)
    {
        var company = await _companyProfile.GetAsync(cancellationToken);
        // Lane 4c (F-55): the uploaded logo, embedded; else the tenant's.
        var logo = await _companyProfile.GetLogoAsync(company.TenantId, cancellationToken);
        var tokens = BuildTokens(a, company, logo);

        var rendered = await _templatedEmail.RenderAsync(
            AssetsEmailCatalog.Module,
            AssetsEmailCatalog.Events.ResponsibilityTerms,
            tokens,
            cancellationToken);

        // Only reachable if the catalog registration were dropped, since the built-in default is the
        // fallback — but a blank document is exactly the failure that gets noticed after it has been
        // signed, so it refuses in words instead.
        if (rendered is null)
            throw AssetsWorkflowException.InvalidState(
                "No template is configured for the asset responsibility document, and no built-in "
                + "default could be resolved. Check the Assets email templates.");

        return (rendered, tokens);
    }

    private static Dictionary<string, string?> BuildTokens(AssetAssignment a, Entities.HR.CompanyProfile company, string? logo)
    {
        var due = a.ExpectedReturnDate;

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // Company identity comes from the per-tenant profile, never from configuration — the
            // provider already falls back to the Tenant record when no profile row exists yet.
            ["CompanyName"] = string.IsNullOrWhiteSpace(company.LegalName) ? null : company.LegalName,
            ["CompanyAddress"] = ComposeAddress(company),
            ["CompanyLogoUrl"] = logo,
            ["CompanyFooter"] = company.DocumentFooterText,
            ["SignatoryName"] = company.DefaultSignatoryName,
            ["SignatoryTitle"] = company.DefaultSignatoryTitle,

            ["LetterDate"] = DateTime.UtcNow.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),

            ["EmployeeName"] = FullName(a),
            ["EmployeeNumber"] = a.Employee?.EmployeeNumber,

            ["AssignmentNumber"] = a.AssignmentNumber,
            ["AssignmentDate"] = a.AssignmentDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
            ["ExpectedReturnDate"] = due?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
                                     ?? "On request / on exit",

            ["AssetNumber"] = a.Asset?.AssetNumber,
            ["AssetName"] = a.Asset?.AssetName,
            ["AssetTypeName"] = a.Asset?.AssetType?.Name,
            ["SerialNumber"] = a.Asset?.SerialNumber,
            ["Specifications"] = a.Asset?.Specifications,
            ["ConditionAtAssignment"] = a.ConditionAtAssignment.ToString(),

            // ⚠ The two flags are passed as tokens so the TEMPLATE decides the wording. "false" is
            // written out rather than omitted: an absent token is falsy anyway, but a template author
            // reading the preview palette needs to see that the token exists and is currently false.
            ["ResponsibleForLoss"] = a.ResponsibleForLoss ? "true" : "false",
            ["ResponsibleForDamage"] = a.ResponsibleForDamage ? "true" : "false",

            ["ReturnUndertaking"] = due is { } d
                ? $"on or before {d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}, or on the "
                  + "day my employment ends, whichever is earlier"
                : "on request, or on the day my employment ends",

            ["AdditionalTerms"] = a.TermsAndConditions,
            ["AcknowledgedOn"] = a.EmployeeAcknowledged && a.AcknowledgementDate is { } ack
                ? ack.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
                : null,
        };
    }

    private static string? ComposeAddress(Entities.HR.CompanyProfile c)
    {
        var parts = new[] { c.RegisteredAddress, c.City, c.Region, c.DigitalAddress }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private static string FullName(AssetAssignment a) =>
        a.Employee is null ? string.Empty : $"{a.Employee.FirstName} {a.Employee.LastName}".Trim();
}
