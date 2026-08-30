using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/rfqs")]
public class RfqsController : ControllerBase
{
    private readonly IRfqService _rfqService;
    private readonly IProcurementRfqControlService _rfqControlService;
    private readonly IRfqInvitationDocumentService _rfqInvitationDocumentService;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<RfqsController> _logger;

    public RfqsController(
        IRfqService rfqService,
        IProcurementRfqControlService rfqControlService,
        IRfqInvitationDocumentService rfqInvitationDocumentService,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<RfqsController> logger)
    {
        _rfqService = rfqService;
        _rfqControlService = rfqControlService;
        _rfqInvitationDocumentService = rfqInvitationDocumentService;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // -----------------------------
    // Internal RFQ management
    // -----------------------------

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT,TDC_EVALUATOR,TDC_OBSERVER,TDC_INTERNAL_AUDIT")]
    public async Task<ActionResult<PagedResult<RfqDto>>> GetRfqs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (pageSize > 100) pageSize = 100;
            var result = await _rfqService.GetRfqsAsync(page, pageSize, search, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RFQs");
            throw;
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT,TDC_EVALUATOR,TDC_OBSERVER,TDC_INTERNAL_AUDIT")]
    public async Task<ActionResult<RfqDetailDto>> GetRfq(Guid id)
    {
        try
        {
            var rfq = await _rfqService.GetRfqByIdAsync(id);
            if (rfq == null) return NotFound($"RFQ with ID {id} not found");
            return Ok(rfq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RFQ {RfqId}", id);
            throw;
        }
    }

    /// <summary>
    /// Generates an RFQ PDF (for printing / emailing) even before sending it to suppliers.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT")]
    public async Task<IActionResult> GetRfqPdf(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var (content, fileName) = await _rfqInvitationDocumentService.GenerateRfqInvitationPdfAsync(id, cancellationToken);
            return File(content, "application/pdf", fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RFQ PDF for {RfqId}", id);
            throw;
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT")]
    public async Task<ActionResult<RfqDetailDto>> UpdateRfq(Guid id, [FromBody] UpdateRfqDto dto)
    {
        try
        {
            var updated = await _rfqService.UpdateRfqAsync(id, dto);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RFQ {RfqId}", id);
            throw;
        }
    }

    [HttpPost("{id:guid}/award")]
    [Authorize]
    public async Task<ActionResult<CreatePurchaseOrdersFromRfqResponseDto>> AwardRfqAndCreatePurchaseOrders(Guid id, [FromBody] CreatePurchaseOrdersFromRfqDto dto)
    {
        try
        {
            var result = await _rfqService.CreatePurchaseOrdersFromAwardAsync(id, dto);
            return Ok(result);
        }
        catch (ProcurementRfqControlAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("RFQ_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRfqControlNotFoundException ex)
        {
            return NotFound(ControlProblem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRfqControlConflictException ex)
        {
            return Conflict(ControlProblem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRfqControlValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (ProcurementTenderDocumentControlAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("TENDER_DOCUMENT_ACCESS_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementTenderDocumentControlConflictException ex)
        {
            return Conflict(ControlProblem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementTenderDocumentControlValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (ProcurementControlEventAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("PROCUREMENT_CONTROL_EVENT_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementControlEventConflictException ex)
        {
            return Conflict(ControlProblem("PROCUREMENT_CONTROL_EVENT_CONFLICT", ex.Message, 409));
        }
        catch (ProcurementControlEventValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (ProcurementAwardReadinessNotFoundException ex)
        {
            return NotFound(AwardReadinessProblem(404, ex.Code, ex.Message));
        }
        catch (ProcurementAwardReadinessAuthorizationException ex)
        {
            return StatusCode(403, AwardReadinessProblem(403, "AWARD_READINESS_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (ProcurementAwardReadinessConflictException ex)
        {
            return Conflict(AwardReadinessProblem(409, ex.Code, ex.Message));
        }
        catch (ProcurementAwardReadinessBlockedException ex)
        {
            return UnprocessableEntity(AwardReadinessProblem(422, ex.Code, ex.Message, ex.Decision));
        }
        catch (ProcurementAwardReadinessValidationException ex)
        {
            return UnprocessableEntity(AwardReadinessProblem(422, ex.Code, ex.Message));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (ProcurementPurchaseOrderSourceValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error awarding RFQ {RfqId}", id);
            throw;
        }
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT")]
    public async Task<IActionResult> SendRfq(Guid id, [FromBody] SendRfqDto dto)
    {
        try
        {
            await _rfqService.SendRfqAsync(id, dto);
            return Ok(new { success = true, message = "RFQ sent" });
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Readiness.DecisionCode, ex.Message, ex.Readiness));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(SourcingProblem(ex.Code, ex.Message));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, SourcingProblem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, status: 403));
        }
        catch (ProcurementRfqControlAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("RFQ_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRfqControlConflictException ex)
        {
            return Conflict(ControlProblem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRfqControlValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (ProcurementTenderDocumentControlAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("TENDER_DOCUMENT_ACCESS_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementTenderDocumentControlConflictException ex)
        {
            return Conflict(ControlProblem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementTenderDocumentControlValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (ProcurementControlEventAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("PROCUREMENT_CONTROL_EVENT_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementControlEventConflictException ex)
        {
            return Conflict(ControlProblem("PROCUREMENT_CONTROL_EVENT_CONFLICT", ex.Message, 409));
        }
        catch (ProcurementControlEventValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (SupplierEligibilityException ex)
        {
            return UnprocessableEntity(new
            {
                status = 422,
                title = "RFQ supplier is not eligible",
                detail = ex.Message,
                instance = Request.Path.Value,
                code = ex.Code,
                correlationId = HttpContext.TraceIdentifier,
                eligibility = ex.Result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            // Unexpected failures must flow through the central exception middleware so the
            // caller receives the standard safe ProblemDetails response and administrators
            // receive a complete, tenant-scoped SystemExceptionLog entry.
            throw;
        }
    }

    private object AwardReadinessProblem(
        int status,
        string code,
        string message,
        object? decision = null) => new
    {
        status,
        title = status == 422 ? "Award is not ready" : "Award-readiness check failed",
        detail = message,
        instance = Request.Path.Value,
        code,
        correlationId = Request.Headers.TryGetValue("X-Correlation-ID", out var supplied) &&
                        !string.IsNullOrWhiteSpace(supplied)
            ? supplied.ToString()
            : HttpContext.TraceIdentifier,
        decision
    };

    [HttpGet("{id:guid}/controls")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT,TDC_EVALUATOR,TDC_OBSERVER,TDC_INTERNAL_AUDIT")]
    public Task<ActionResult<ProcurementRfqControlDto>> GetControls(Guid id) =>
        ExecuteControlAsync(() => _rfqControlService.GetAsync(id));

    [HttpPost("{id:guid}/opening-register")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,TDC_PROCUREMENT_OFFICER,TDC_SENIOR_PROCUREMENT_OFFICER,TDC_HEAD_OF_PROCUREMENT")]
    public Task<ActionResult<ProcurementRfqOpeningRegisterDto>> CompleteOpening(
        Guid id, [FromBody] CompleteProcurementRfqOpeningRequest request) =>
        ExecuteControlAsync(() => _rfqControlService.CompleteOpeningAsync(id, request, HttpContext.TraceIdentifier));

    [HttpPut("{id:guid}/evaluation")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,TDC_EVALUATOR")]
    public Task<ActionResult<ProcurementRfqEvaluationDto>> SaveEvaluation(
        Guid id, [FromBody] SaveProcurementRfqEvaluationRequest request) =>
        ExecuteControlAsync(() => _rfqControlService.SaveEvaluationAsync(id, request, HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/evaluation/submit")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,TDC_EVALUATOR")]
    public Task<ActionResult<ProcurementRfqEvaluationDto>> SubmitEvaluation(
        Guid id, [FromBody] SubmitProcurementRfqEvaluationRequest request) =>
        ExecuteControlAsync(() => _rfqControlService.SubmitEvaluationAsync(id, request, HttpContext.TraceIdentifier));

    [HttpPost("{id:guid}/evaluation/decision")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,TDC_HEAD_OF_PROCUREMENT,TDC_ETC_MEMBER,TDC_CENTRAL_REVIEW_MEMBER,TDC_BOARD_APPROVER,TDC_MANAGING_DIRECTOR")]
    public Task<ActionResult<ProcurementRfqEvaluationDto>> DecideEvaluation(
        Guid id, [FromBody] DecideProcurementRfqEvaluationRequest request) =>
        ExecuteControlAsync(() => _rfqControlService.DecideEvaluationAsync(id, request, HttpContext.TraceIdentifier));

    // -----------------------------
    // Supplier portal endpoints
    // -----------------------------

    [HttpGet("my-rfqs")]
    public async Task<ActionResult<List<RfqDto>>> GetMyRfqs()
    {
        try
        {
            if (!_currentUserProvider.IsExternalUser)
            {
                return Forbid();
            }

            var businessPartner = await ResolveCurrentBusinessPartnerAsync();
            if (businessPartner == null)
                return Ok(new List<RfqDto>());

            var list = await _rfqService.GetSupplierRfqsAsync(businessPartner.Id, _currentUserProvider.TenantId);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier RFQs");
            throw;
        }
    }

    [HttpGet("{id:guid}/my-view")]
    public async Task<ActionResult<RfqDetailDto>> GetMyRfqDetail(Guid id)
    {
        try
        {
            if (!_currentUserProvider.IsExternalUser)
            {
                return Forbid();
            }

            var businessPartner = await ResolveCurrentBusinessPartnerAsync();
            if (businessPartner == null)
                return NotFound();

            var detail = await _rfqService.GetSupplierRfqDetailAsync(id, businessPartner.Id, _currentUserProvider.TenantId);
            if (detail == null) return NotFound();

            return Ok(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier RFQ detail {RfqId}", id);
            throw;
        }
    }

    [HttpPost("{id:guid}/my-view/opened")]
    public async Task<ActionResult<RfqDetailDto>> RecordMyRfqOpened(Guid id)
    {
        if (!_currentUserProvider.IsExternalUser)
            return Forbid();

        var businessPartner = await ResolveCurrentBusinessPartnerAsync();
        if (businessPartner == null)
            return NotFound();

        var detail = await _rfqService.RecordSupplierRfqOpenedAsync(
            id,
            businessPartner.Id,
            _currentUserProvider.UserId,
            _currentUserProvider.TenantId);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/quote")]
    public async Task<ActionResult<RfqQuoteDto>> SubmitQuote(Guid id, [FromBody] SubmitRfqQuoteDto dto)
    {
        try
        {
            if (!_currentUserProvider.IsExternalUser)
            {
                return Forbid();
            }

            var businessPartner = await ResolveCurrentBusinessPartnerAsync();
            if (businessPartner == null)
                return NotFound();

            var quote = await _rfqService.SubmitQuoteAsync(
                id,
                businessPartner.Id,
                _currentUserProvider.UserId,
                _currentUserProvider.TenantId,
                dto);

            return Ok(quote);
        }
        catch (ProcurementRfqControlAuthorizationException ex)
        {
            return StatusCode(403, ControlProblem("RFQ_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRfqControlNotFoundException ex)
        {
            return NotFound(ControlProblem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRfqControlConflictException ex)
        {
            return Conflict(ControlProblem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRfqControlValidationException ex)
        {
            return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DbUpdateException ex) when (IsDuplicateRfqQuoteItem(ex))
        {
            throw new ConflictException(
                "We couldn’t save your quote because pricing for one or more RFQ lines already exists. Please refresh the page and try submitting again.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting RFQ quote for {RfqId}", id);
            throw;
        }
    }

    private static bool IsDuplicateRfqQuoteItem(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message ?? string.Empty;
        return message.Contains("IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("RequestForQuotationQuoteItems", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Cannot insert duplicate key row", StringComparison.OrdinalIgnoreCase));
    }

    private ProblemDetails SourcingProblem(
        string code,
        string detail,
        PurchaseRequisitionSourcingReadinessDto? readiness = null,
        int status = 422)
    {
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail, Instance = HttpContext.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        if (readiness is not null) problem.Extensions["readiness"] = readiness;
        return problem;
    }

    private async Task<ActionResult<T>> ExecuteControlAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ProcurementRfqControlAuthorizationException ex) { return StatusCode(403, ControlProblem("RFQ_CONTROL_FORBIDDEN", ex.Message, 403)); }
        catch (ProcurementRfqControlNotFoundException ex) { return NotFound(ControlProblem(ex.Code, ex.Message, 404)); }
        catch (ProcurementRfqControlConflictException ex) { return Conflict(ControlProblem(ex.Code, ex.Message, 409)); }
        catch (ProcurementRfqControlValidationException ex) { return UnprocessableEntity(ControlProblem(ex.Code, ex.Message, 422)); }
        catch (DbUpdateConcurrencyException) { return Conflict(ControlProblem("RFQ_CONTROL_VERSION_CONFLICT", "The RFQ control record changed. Reload before continuing.", 409)); }
    }

    private ProblemDetails ControlProblem(string code, string detail, int status)
    {
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail, Instance = HttpContext.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        return problem;
    }

    private async Task<Core.Entities.Procurement.BusinessPartner?> ResolveCurrentBusinessPartnerAsync()
    {
        // First, try to resolve as main portal owner.
        var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

        // If not found, try sub-user mapping.
        if (businessPartner == null)
        {
            var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
            if (businessPartnerUser != null && businessPartnerUser.IsActive)
            {
                businessPartner = businessPartnerUser.BusinessPartner;
            }
        }

        return businessPartner;
    }
}
