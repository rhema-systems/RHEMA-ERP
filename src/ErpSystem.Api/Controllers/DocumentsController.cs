using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    public static readonly IReadOnlyDictionary<string, string> FinanceDocumentPolicies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // A journal voucher is the printable representation of the same tenant-scoped journal
        // already exposed by JournalEntryController under Finance.Read. Keep print/export aligned
        // with that read boundary; analytical reports and sensitive payment documents retain their
        // stronger export/issue permissions below.
        [DocumentTypes.FinanceJournalVoucher] = FinancePermissions.ViewFinance,
        // AP vouchers contain supplier banking references, approval identities and evidence
        // hashes. Route them through the same explicit Finance export/print permission as other
        // controlled accounting documents instead of relying only on authenticated access.
        [DocumentTypes.FinanceApPaymentVoucher] = FinancePermissions.ExportFinanceReports,
        // These two builders persist a one-original/many-replacement issue register. Ordinary
        // cashiers may issue originals; replacement requests are elevated separately below.
        [DocumentTypes.FinanceCashBankPaymentSlip] = FinancePermissions.IssueCashBankDocuments,
        [DocumentTypes.FinanceArCustomerReceipt] = FinancePermissions.IssueCashBankDocuments,
        // Supplier statements disclose counterparty balances and payment/WHT history. Require the
        // explicit Finance export permission for both PDF and native spreadsheet renderings.
        [DocumentTypes.FinanceApSupplierStatement] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceApAgingReport] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceApCashRequirements] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceApMatchExceptionReport] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceApProcurementReconciliation] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceArAgingReport] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceArCustomerStatement] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceCashPositionReport] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceTaxInputRegister] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceTaxOutputRegister] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceTaxVatReconciliation] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceTaxWhtPayable] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceTaxWhtCertificateRegister] = FinancePermissions.ExportFinanceReports,
        // Each statutory WHT certificate PDF is an issued, retained controlled document rather
        // than an analytical report. Align original and replacement access with the Finance user
        // who owns the certificate lifecycle; the builder separately enforces posted source data.
        [DocumentTypes.FinanceTaxWhtCertificate] = FinancePermissions.ManageTaxConfiguration,
        [DocumentTypes.FinanceTaxWhtRemittanceRegister] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceBudgetConsolidated] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceBudgetScenarioComparison] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceTrialBalance] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceIncomeStatement] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceBalanceSheet] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceCashFlowStatement] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceMultiCurrencyDetail] = FinancePermissions.ExportFinanceReports,
        [DocumentTypes.FinanceDetailedLedger] = FinancePermissions.ExportFinanceReports,
        // Close packs contain signed declarations, exception evidence and user identities. They
        // pass through the generic renderer, so keep the Finance export policy explicit here.
        [DocumentTypes.FinanceClosePack] = FinancePermissions.ExportFinanceReports
    };
    public static readonly IReadOnlyDictionary<string, string> QuantitySurveyDocumentPolicies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DocumentTypes.QuantitySurveyEscalationDisputeAuditPack] = QuantitySurveyAccessControlRegistry.AuditRead
        };

    private readonly IDocumentOutputService _documentOutputService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentOutputService documentOutputService,
        IAuthorizationService authorizationService,
        ILogger<DocumentsController> logger)
    {
        _documentOutputService = documentOutputService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    [HttpGet("{documentType}/{entityId:guid}")]
    public async Task<IActionResult> RenderDocument(
        string documentType,
        Guid entityId,
        [FromQuery] string format = "pdf",
        [FromQuery] string copyType = "Original",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authorizationFailure = await AuthorizeDocumentRenderAsync(documentType, copyType);
            if (authorizationFailure != null)
            {
                return authorizationFailure;
            }

            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = documentType,
                EntityId = entityId,
                Format = format,
                CopyType = copyType
            }, cancellationToken);

            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (QuantitySurveyEscalationDisputeNotFoundException ex)
        {
            return NotFound(SafeQuantitySurveyProblem(404, ex.Message));
        }
        catch (QuantitySurveyEscalationDisputeConflictException ex)
        {
            return Conflict(SafeQuantitySurveyProblem(409, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render document {DocumentType} for entity {EntityId}", documentType, entityId);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Issues a controlled transaction document. POST is used by the Finance workspaces because
    /// issuance appends an immutable register row and replacement copies require a reason. The
    /// older GET renderer remains available to established read-only report builders.
    /// </summary>
    [HttpPost("{documentType}/{entityId:guid}/issue")]
    public async Task<IActionResult> IssueControlledDocument(
        string documentType,
        Guid entityId,
        [FromBody] ControlledDocumentIssueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authorizationFailure = await AuthorizeDocumentRenderAsync(documentType, request.CopyType);
            if (authorizationFailure != null)
            {
                return authorizationFailure;
            }

            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = documentType,
                EntityId = entityId,
                Format = request.Format,
                CopyType = request.CopyType,
                Options = string.IsNullOrWhiteSpace(request.ReplacementReason)
                    ? null
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["replacementReason"] = request.ReplacementReason
                    }
            }, cancellationToken);

            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue controlled document {DocumentType} for entity {EntityId}", documentType, entityId);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{documentType}/{entityId:guid}/issues")]
    public async Task<IActionResult> GetControlledDocumentIssues(
        string documentType,
        Guid entityId,
        [FromServices] IFinanceControlledDocumentIssueService issueService,
        CancellationToken cancellationToken = default)
    {
        if (!FinanceDocumentPolicies.ContainsKey(documentType))
            return Forbid();
        var authorizationFailure = await AuthorizeDocumentRenderAsync(documentType, ControlledDocumentCopyTypes.Original);
        if (authorizationFailure != null)
            return authorizationFailure;

        try
        {
            return Ok(await issueService.GetSummaryAsync(documentType, entityId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("controlled-issues/{issueId:guid}")]
    public async Task<IActionResult> DownloadRetainedControlledDocument(
        Guid issueId,
        [FromServices] IFinanceControlledDocumentIssueService issueService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Authorize from tenant-scoped metadata before private bytes are read. Retrieval then
            // re-hashes the artifact so a storage mutation cannot silently alter statutory proof.
            var issue = await issueService.GetIssueAsync(issueId, cancellationToken);
            if (!FinanceDocumentPolicies.ContainsKey(issue.DocumentType))
                return Forbid();
            var authorizationFailure = await AuthorizeDocumentRenderAsync(issue.DocumentType, issue.CopyType);
            if (authorizationFailure != null)
                return authorizationFailure;

            var retained = await issueService.GetRetainedAsync(issueId, cancellationToken);
            return File(retained.Content, retained.ContentType, retained.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{documentType}")]
    public async Task<IActionResult> RenderParameterizedDocument(
        string documentType,
        [FromQuery] string format = "pdf",
        [FromQuery] string copyType = "Original",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authorizationFailure = await AuthorizeDocumentRenderAsync(documentType, copyType);
            if (authorizationFailure != null)
            {
                return authorizationFailure;
            }

            var options = Request.Query
                .Where(pair => !string.Equals(pair.Key, "format", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(pair.Key, "copyType", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = documentType,
                EntityId = Guid.Empty,
                Format = format,
                CopyType = copyType,
                Options = options
            }, cancellationToken);

            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render parameterized document {DocumentType}", documentType);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private async Task<IActionResult?> AuthorizeDocumentRenderAsync(string documentType, string? copyType)
    {
        if (!FinanceDocumentPolicies.TryGetValue(documentType ?? string.Empty, out var policy) &&
            !QuantitySurveyDocumentPolicies.TryGetValue(documentType ?? string.Empty, out policy))
        {
            return null;
        }

        // Replacement copies are more sensitive than first issuance because they can be used to
        // present the same transaction evidence twice. Only the controlled cash/bank documents
        // use this elevated policy; existing reports retain their established export permission.
        if ((string.Equals(documentType, DocumentTypes.FinanceCashBankPaymentSlip, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(documentType, DocumentTypes.FinanceArCustomerReceipt, StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(copyType, ControlledDocumentCopyTypes.Replacement, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(copyType, "Reprint", StringComparison.OrdinalIgnoreCase)))
        {
            policy = FinancePermissions.ReprintCashBankDocuments;
        }

        // The generic document renderer bypasses Finance controller conventions, so Finance.* documents must
        // explicitly require the same export/print permission before any builder can render sensitive GL output.
        var authorization = await _authorizationService.AuthorizeAsync(User, policy);
        return authorization.Succeeded ? null : Forbid();
    }

    private ProblemDetails SafeQuantitySurveyProblem(int status, string detail) => new()
    {
        Status = status,
        Title = status == 404 ? "QS escalation dispute not found" : "QS escalation dispute export conflict",
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-escalation-dispute-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_ESCALATION_DISPUTE_{status}",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
