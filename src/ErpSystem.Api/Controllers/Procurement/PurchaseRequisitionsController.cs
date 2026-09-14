using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Procurement;

/// <summary>
/// API controller for managing purchase requisitions
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseRequisitionsController : ControllerBase
{
    private readonly IPurchaseRequisitionRepository _purchaseRequisitionRepository;
    private readonly IPurchaseRequisitionItemRepository _purchaseRequisitionItemRepository;
    private readonly IRfqService _rfqService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IWorkflowService _workflowService;
    private readonly IProcurementRequisitionLinkageService _linkageService;
    private readonly IProcurementRequisitionSubmissionControlService _submissionControlService;
    private readonly IProcurementRequisitionBudgetControlService _budgetControlService;
    private readonly IProcurementRequisitionAuthorityRouteService _authorityRouteService;
    private readonly IProcurementRequisitionSourcingReleaseService _sourcingReleaseService;
    private readonly IProcurementAccessControlService _procurementAccessControlService;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<PurchaseRequisitionsController> _logger;

    public PurchaseRequisitionsController(
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IPurchaseRequisitionItemRepository purchaseRequisitionItemRepository,
        IRfqService rfqService,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IWorkflowService workflowService,
        IProcurementRequisitionLinkageService linkageService,
        IProcurementRequisitionSubmissionControlService submissionControlService,
        IProcurementRequisitionBudgetControlService budgetControlService,
        IProcurementRequisitionAuthorityRouteService authorityRouteService,
        IProcurementRequisitionSourcingReleaseService sourcingReleaseService,
        IProcurementAccessControlService procurementAccessControlService,
        IAppEventBus appEventBus,
        ILogger<PurchaseRequisitionsController> logger)
    {
        _purchaseRequisitionRepository = purchaseRequisitionRepository;
        _purchaseRequisitionItemRepository = purchaseRequisitionItemRepository;
        _rfqService = rfqService;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _workflowService = workflowService;
        _linkageService = linkageService;
        _submissionControlService = submissionControlService;
        _budgetControlService = budgetControlService;
        _authorityRouteService = authorityRouteService;
        _sourcingReleaseService = sourcingReleaseService;
        _procurementAccessControlService = procurementAccessControlService;
        _appEventBus = appEventBus;
        _logger = logger;
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PurchaseRequisitionDetailDto>> UpdatePurchaseRequisition(
        Guid id,
        [FromBody] UpdatePurchaseRequisitionDto updateDto,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var contractError = ValidateDraftContract(updateDto);
            if (contractError is not null) return UnprocessableEntity(Problem("PR_CONTRACT_INVALID", contractError, 422));
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition is null)
                return NotFound(Problem("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.", 404));
            if (!string.Equals(requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return Conflict(Problem("PR_NOT_DRAFT", "Only a Draft purchase requisition can be edited.", 409));
            if (!TryDecodeRowVersion(updateDto.RowVersion, out var suppliedVersion))
                return UnprocessableEntity(Problem("ROW_VERSION_INVALID", "RowVersion must be a valid base64 value.", 422));
            if (!requisition.RowVersion.SequenceEqual(suppliedVersion))
                return Conflict(Problem("ROW_VERSION_STALE", "The purchase requisition changed after it was loaded. Refresh and try again.", 409));

            NormalizePlanItemLineage(updateDto);
            var before = _linkageService.Map(requisition);
            var organizationUnit = await ResolveOrganizationUnitAsync(
                updateDto.OrganizationUnitId,
                updateDto.Linkage.SourcePlanItemId,
                requisition.TenantId,
                cancellationToken);
            await ApplyAuthoritativeInventoryPricingAsync(
                updateDto.Items,
                requisition.TenantId,
                cancellationToken);
            requisition.Currency = await ResolveRequisitionCurrencyAsync(
                updateDto.Currency,
                requisition.TenantId,
                cancellationToken);
            await _linkageService.PrepareAsync(requisition, updateDto.Linkage, CorrelationId, cancellationToken);

            requisition.RequiredDate = updateDto.RequiredDate;
            requisition.Priority = updateDto.Priority.Trim();
            requisition.OrganizationUnitId = organizationUnit.Id;
            requisition.Department = organizationUnit.Name;
            requisition.Justification = TrimOrNull(updateDto.Justification, 2000);
            requisition.Notes = TrimOrNull(updateDto.Notes, 2000);
            requisition.TotalAmount = updateDto.Items.Sum(item => item.Quantity * item.EstimatedUnitPrice);
            requisition.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    var existingItems = (await _purchaseRequisitionItemRepository.GetItemsByRequisitionIdAsync(id)).ToList();
                    foreach (var item in existingItems)
                        await _unitOfWork.Repository<PurchaseRequisitionItem>().DeleteAsync(item);
                    foreach (var itemDto in updateDto.Items)
                        await _purchaseRequisitionItemRepository.CreateItemAsync(CreateItem(requisition.TenantId, requisition.Id, itemDto));
                    await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _linkageService.RecordMutationAsync(
                        requisition, "Updated", before, CorrelationId,
                        "Draft purchase requisition and governance linkage were updated.", cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);

            return Ok(await GetPurchaseRequisitionDetailDto(id));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(Problem("ROW_VERSION_STALE", "The purchase requisition changed after it was loaded. Refresh and try again.", 409));
        }
        catch (ProcurementRequisitionLinkageAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_LINKAGE_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionLinkageNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionLinkageConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionLinkageValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase requisition {RequisitionId}", id);
            return StatusCode(500, Problem("PR_UPDATE_FAILED", "The purchase requisition could not be updated.", 500));
        }
    }

    [HttpGet("linkage-options")]
    public async Task<ActionResult<PurchaseRequisitionLinkageOptionsDto>> GetLinkageOptions(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _linkageService.GetOptionsAsync(cancellationToken));
        }
        catch (ProcurementRequisitionLinkageAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_LINKAGE_FORBIDDEN", ex.Message, 403));
        }
    }

    [HttpGet("{id}/linkage-history")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRequisitionLinkageHistoryDto>>> GetLinkageHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _linkageService.GetHistoryAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionLinkageAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_LINKAGE_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionLinkageNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/submission-readiness")]
    public async Task<ActionResult<PurchaseRequisitionSubmissionReadinessDto>> GetSubmissionReadiness(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _submissionControlService.GetReadinessAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionSubmissionAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SUBMISSION_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSubmissionNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/submission-control-history")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRequisitionSubmissionControlHistoryDto>>> GetSubmissionControlHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _submissionControlService.GetHistoryAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionSubmissionAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SUBMISSION_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSubmissionNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/budget-readiness")]
    public async Task<ActionResult<PurchaseRequisitionBudgetReadinessDto>> GetBudgetReadiness(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _budgetControlService.GetReadinessAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionBudgetAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_BUDGET_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionBudgetNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/budget-control-history")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRequisitionBudgetControlHistoryDto>>> GetBudgetControlHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _budgetControlService.GetHistoryAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionBudgetAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_BUDGET_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionBudgetNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/authority-readiness")]
    public async Task<ActionResult<PurchaseRequisitionAuthorityReadinessDto>> GetAuthorityReadiness(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authorityRouteService.GetReadinessAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionAuthorityAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_AUTHORITY_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionAuthorityNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/authority-route-history")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRequisitionAuthorityRouteHistoryDto>>> GetAuthorityRouteHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authorityRouteService.GetHistoryAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionAuthorityAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_AUTHORITY_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionAuthorityNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/sourcing-readiness")]
    public async Task<ActionResult<PurchaseRequisitionSourcingReadinessDto>> GetSourcingReadiness(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sourcingReleaseService.GetReadinessAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSourcingNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpGet("{id}/sourcing-release-history")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRequisitionSourcingReleaseDto>>> GetSourcingReleaseHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sourcingReleaseService.GetHistoryAsync(id, cancellationToken));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSourcingNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
    }

    [HttpPost("{id}/sourcing-release")]
    public async Task<ActionResult<PurchaseRequisitionSourcingReleaseDto>> ReleaseForSourcing(
        Guid id,
        [FromBody] ReleasePurchaseRequisitionForSourcingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            return Ok(await _sourcingReleaseService.ReleaseAsync(id, request.Reason, CorrelationId, cancellationToken));
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSourcingNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            var problem = Problem(ex.Readiness.DecisionCode, ex.Message, 422);
            problem.Extensions["readiness"] = ex.Readiness;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (ProcurementRequisitionSourcingConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> ExportPurchaseRequisition(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition is null)
                return NotFound(Problem("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.", 404));
            var detail = await GetPurchaseRequisitionDetailDto(id);
            await _linkageService.RecordExportAsync(requisition, CorrelationId, cancellationToken);
            var export = new PurchaseRequisitionExportDto
            {
                ExportedAtUtc = DateTime.UtcNow,
                TenantId = requisition.TenantId,
                RequisitionId = requisition.Id,
                RequisitionNumber = requisition.RequisitionNumber,
                RequisitionDate = requisition.RequisitionDate,
                Status = requisition.Status,
                RequestedByName = detail.RequestedByName,
                Department = requisition.Department,
                TotalAmount = requisition.TotalAmount,
                Linkage = detail.Linkage,
                Items = detail.Items
            };
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            return File(Encoding.UTF8.GetBytes(json), "application/json", $"{requisition.RequisitionNumber}-linkage.json");
        }
        catch (ProcurementRequisitionLinkageAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_EXPORT_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementControlEventConflictException ex)
        {
            return Conflict(Problem("PR_EXPORT_AUDIT_CONFLICT", ex.Message, 409));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting purchase requisition {RequisitionId}", id);
            return StatusCode(500, Problem("PR_EXPORT_FAILED", "The purchase requisition export could not be generated.", 500));
        }
    }

    /// <summary>
    /// Gets all purchase requisitions with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseRequisitionSummaryDto>>> GetPurchaseRequisitions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? department = null,
        [FromQuery] Guid? sourcePlanId = null)
    {
        try
        {
            var requisitions = await _purchaseRequisitionRepository.GetRequisitionsAsync(
                page, pageSize, search, status, priority, startDate, endDate, department, sourcePlanId);

            var requisitionDtos = requisitions.Items.Select(MapSummary).ToList();

            // Provide more accurate UX for pending approvals: show the actual current workflow step name.
            var pendingDtos = requisitionDtos
                .Where(d => d.Status == "Pending Approval" || d.Status == "Submitted")
                .ToList();

            if (pendingDtos.Count > 0)
            {
                await Task.WhenAll(pendingDtos.Select(async dto =>
                {
                    try
                    {
                        var currentStep = await _workflowService.GetCurrentWorkflowStepAsync("PurchaseRequisition", dto.Id);
                        dto.CurrentWorkflowStepName = currentStep?.StepName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to resolve current workflow step for PR {RequisitionId}", dto.Id);
                    }
                }));
            }

            var result = new PagedResult<PurchaseRequisitionSummaryDto>
            {
                Items = requisitionDtos,
                TotalCount = requisitions.TotalCount,
                Page = requisitions.Page,
                PageSize = requisitions.PageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase requisitions");
            return StatusCode(500, "An error occurred while retrieving purchase requisitions");
        }
    }

    /// <summary>
    /// Gets a purchase requisition by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseRequisitionDetailDto>> GetPurchaseRequisition(Guid id)
    {
        try
        {
            var requisitionDto = await GetPurchaseRequisitionDetailDto(id);
            if (requisitionDto == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            if (requisitionDto.Status == "Pending Approval" || requisitionDto.Status == "Submitted")
            {
                try
                {
                    var currentStep = await _workflowService.GetCurrentWorkflowStepAsync("PurchaseRequisition", requisitionDto.Id);
                    requisitionDto.CurrentWorkflowStepName = currentStep?.StepName;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve current workflow step for PR {RequisitionId}", requisitionDto.Id);
                }
            }

            return Ok(requisitionDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase requisition {RequisitionId}", id);
            return StatusCode(500, "An error occurred while retrieving the purchase requisition");
        }
    }

    /// <summary>
    /// Creates a new purchase requisition
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseRequisitionDetailDto>> CreatePurchaseRequisition(
        [FromBody] CreatePurchaseRequisitionDto createDto,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var contractError = ValidateDraftContract(createDto);
            if (contractError is not null) return UnprocessableEntity(Problem("PR_CONTRACT_INVALID", contractError, 422));
            if (!_currentUserProvider.IsAuthenticated || _currentUserProvider.UserId == Guid.Empty)
                return Unauthorized(Problem("AUTHENTICATION_REQUIRED", "An authenticated user is required.", 401));

            var tenantId = _tenantContext.GetCurrentTenantId();
            NormalizePlanItemLineage(createDto);
            var organizationUnit = await ResolveOrganizationUnitAsync(
                createDto.OrganizationUnitId,
                createDto.Linkage.SourcePlanItemId,
                tenantId,
                cancellationToken);
            await ApplyAuthoritativeInventoryPricingAsync(
                createDto.Items,
                tenantId,
                cancellationToken);
            var requisitionNumber = await _purchaseRequisitionRepository.GenerateRequisitionNumberAsync();
            var totalAmount = createDto.Items.Sum(item => item.Quantity * item.EstimatedUnitPrice);
            var requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequisitionNumber = requisitionNumber,
                RequisitionDate = DateTime.UtcNow,
                RequestedById = _currentUserProvider.UserId,
                RequiredDate = createDto.RequiredDate,
                Status = "Draft",
                Priority = createDto.Priority,
                OrganizationUnitId = organizationUnit.Id,
                Department = organizationUnit.Name,
                Justification = createDto.Justification,
                Notes = createDto.Notes,
                TotalAmount = totalAmount,
                Currency = await ResolveRequisitionCurrencyAsync(createDto.Currency, tenantId, cancellationToken),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _linkageService.PrepareAsync(requisition, createDto.Linkage, CorrelationId, cancellationToken);

            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    await _purchaseRequisitionRepository.CreateRequisitionAsync(requisition);
                    foreach (var itemDto in createDto.Items)
                    {
                        await _purchaseRequisitionItemRepository.CreateItemAsync(CreateItem(tenantId, requisition.Id, itemDto));
                    }
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _linkageService.RecordMutationAsync(
                        requisition, "Created", null, CorrelationId,
                        "Purchase requisition and its governance linkage were created.", cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);

            try
            {
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = "PurchaseRequisition",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = requisition.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>
                    {
                        ["PurchaseRequisitionId"] = requisition.Id,
                        ["RequisitionNumber"] = requisition.RequisitionNumber ?? string.Empty,
                        ["Status"] = requisition.Status ?? string.Empty,
                        ["Priority"] = requisition.Priority ?? string.Empty,
                        ["Department"] = requisition.Department ?? string.Empty,
                        ["TotalAmount"] = requisition.TotalAmount,
                        ["RequestedById"] = requisition.RequestedById
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish PurchaseRequisition.Created entity activity event for requisition {RequisitionId}", requisition.Id);
            }

            var createdRequisition = await GetPurchaseRequisitionDetailDto(requisition.Id);
            return CreatedAtAction(nameof(GetPurchaseRequisition), new { id = requisition.Id }, createdRequisition);
        }
        catch (ProcurementRequisitionLinkageAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_LINKAGE_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionLinkageNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionLinkageConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionLinkageValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase requisition");
            return StatusCode(500, "An error occurred while creating the purchase requisition");
        }
    }

    /// <summary>
    /// Updates purchase requisition status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdatePurchaseRequisitionStatus(Guid id, [FromBody] UpdateStatusDto statusDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var normalizedStatus = statusDto.Status.Trim().Replace(" ", string.Empty);
            if (normalizedStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase) ||
                normalizedStatus.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase) ||
                normalizedStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
                normalizedStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(Problem(
                    "PR_WORKFLOW_STATUS_REQUIRES_ACTION",
                    "Workflow-controlled requisition statuses cannot be assigned directly. Use the submit or approval action so required-data, budget-availability, and workflow controls execute.",
                    409));
            }

            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound(Problem("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.", 404));
            }

            var isCancellation = normalizedStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                                 normalizedStatus.Equals("Canceled", StringComparison.OrdinalIgnoreCase);
            if (isCancellation)
            {
                await _unitOfWork.ExecuteInStrategyAsync(async () =>
                {
                    await _unitOfWork.BeginTransactionAsync(HttpContext.RequestAborted);
                    try
                    {
                        // The linkage trigger permits the budget snapshot to clear only when the
                        // same atomic write carries the requisition into a release-eligible status.
                        requisition.Status = statusDto.Status;
                        requisition.UpdatedAt = DateTime.UtcNow;
                        await _budgetControlService.ReleaseAsync(
                            requisition,
                            "Purchase requisition cancelled.",
                            "procurement.requisition.create",
                            CorrelationId,
                            HttpContext.RequestAborted);
                        await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
                        await _unitOfWork.CommitAsync(HttpContext.RequestAborted);
                    }
                    catch
                    {
                        await _unitOfWork.RollbackAsync(HttpContext.RequestAborted);
                        throw;
                    }
                }, HttpContext.RequestAborted);
            }
            else
            {
                await _purchaseRequisitionRepository.UpdateStatusAsync(id, statusDto.Status);
                await _unitOfWork.SaveChangesAsync();
            }

            return NoContent();
        }
        catch (ProcurementRequisitionBudgetAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_BUDGET_RELEASE_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionBudgetNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionBudgetConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionBudgetValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase requisition status {RequisitionId}", id);
            return StatusCode(500, "An error occurred while updating purchase requisition status");
        }
    }

    /// <summary>
    /// Approves a purchase requisition
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePurchaseRequisition(Guid id, [FromBody] ApprovalDto approvalDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_currentUserProvider.IsAuthenticated)
            {
                return Unauthorized("User is not authenticated");
            }

            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            if (requisition.Status != "Pending Approval" &&
                requisition.Status != "Submitted")
            {
                return BadRequest($"Purchase requisition cannot be approved in current status: {requisition.Status}");
            }

            var userId = _currentUserProvider.UserId;
            if (userId == Guid.Empty)
            {
                return Unauthorized("User identifier claim is missing or invalid");
            }

            if (approvalDto.Approved && requisition.RequestedById == userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, Problem(
                    "PR_SELF_APPROVAL_FORBIDDEN",
                    "The requisition requester cannot approve their own purchase requisition.",
                    StatusCodes.Status403Forbidden));
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync("PurchaseRequisition", id, userId);
            if (!canApprove)
            {
                return StatusCode(403, "You are not assigned as an approver for the current workflow step");
            }

            if (!_currentUserProvider.HasRole(Constants.Roles.SuperAdmin))
            {
                var capability = await _procurementAccessControlService.EnforceCapabilityAsync(
                    new ProcurementAccessCapabilityRequest
                    {
                        PermissionCode = "procurement.requisition.approve",
                        SourceType = "PurchaseRequisition",
                        SourceReference = requisition.RequisitionNumber
                    },
                    CorrelationId,
                    HttpContext.RequestAborted);
                if (!capability.Allowed)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, Problem(
                        "PR_APPROVAL_CAPABILITY_REQUIRED",
                        $"The effective procurement.requisition.approve capability is required for this purchase requisition approval action. {capability.Message}",
                        StatusCodes.Status403Forbidden));
                }
            }

            var action = approvalDto.Approved ? "approve" : "reject";
            var comments = approvalDto.Comments;
            if (!approvalDto.Approved && string.IsNullOrWhiteSpace(comments))
            {
                comments = approvalDto.RejectionReason;
            }

            if (!approvalDto.Approved && string.IsNullOrWhiteSpace(comments))
            {
                return BadRequest("Rejection comment is required");
            }

            WorkflowIntegrationResult? workflowResult = null;
            PurchaseRequisitionBudgetReleaseDto? budgetRelease = null;
            PurchaseRequisitionBudgetReadinessDto? approvalBudgetReadiness = null;
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(HttpContext.RequestAborted);
                try
                {
                    if (approvalDto.Approved)
                    {
                        // PR approval is an availability checkpoint only. The
                        // downstream PO/contract transaction owns reservation
                        // and formal commitment, but an approver must not approve
                        // against a budget that became ineffective or insufficient
                        // after submission.
                        // The workflow service has already confirmed that this actor is the
                        // assigned approver. Revalidate the tenant-scoped linked budget without
                        // imposing the separate procurement-dashboard reader-role requirement.
                        approvalBudgetReadiness = await _budgetControlService.GetLinkedControlReadinessAsync(
                            requisition.Id,
                            HttpContext.RequestAborted);
                        if (!approvalBudgetReadiness.CanReserve)
                        {
                            throw new ProcurementRequisitionBudgetValidationException(
                                approvalBudgetReadiness.DecisionCode,
                                approvalBudgetReadiness.Message);
                        }
                    }

                    workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                        "PurchaseRequisition",
                        id,
                        userId,
                        action,
                        comments);

                    var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("PurchaseRequisition");
                    statusAdapter.ApplyApprovalOutcome(requisition, workflowResult.Outcome, userId, approvalDto.RejectionReason);

                    if (!approvalDto.Approved)
                    {
                        budgetRelease = await _budgetControlService.ReleaseAsync(
                            requisition,
                            comments!,
                            "procurement.requisition.approve",
                            CorrelationId,
                            HttpContext.RequestAborted);
                    }

                    await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
                    await _unitOfWork.CommitAsync(HttpContext.RequestAborted);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(HttpContext.RequestAborted);
                    throw;
                }
            }, HttpContext.RequestAborted);

            if (workflowResult is null)
                return Conflict(Problem("PR_WORKFLOW_RESULT_MISSING", "The workflow did not return an approval outcome.", 409));

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var activity = approvalDto.Approved ? "Approved" : "Rejected";
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = requisition.TenantId,
                    EntityType = "PurchaseRequisition",
                    Activity = activity,
                    Audience = "Internal",
                    EntityId = requisition.Id,
                    TriggeredByUserId = userId,
                    Data = new Dictionary<string, object>
                    {
                        ["PurchaseRequisitionId"] = requisition.Id,
                        ["RequisitionNumber"] = requisition.RequisitionNumber ?? string.Empty,
                        ["Status"] = requisition.Status ?? string.Empty,
                        ["WorkflowInstanceId"] = workflowResult.ExecutionResult.WorkflowInstanceId?.ToString() ?? string.Empty,
                        ["WorkflowOutcome"] = workflowResult.Outcome.ToString(),
                        ["Comments"] = comments ?? string.Empty,
                        ["RejectionReason"] = approvalDto.RejectionReason ?? string.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish PurchaseRequisition approval entity activity event for requisition {RequisitionId}", requisition.Id);
            }

            return Ok(new
            {
                success = true,
                message = approvalDto.Approved ? "Purchase requisition approved" : "Purchase requisition rejected",
                data = new
                {
                    id = requisition.Id,
                    status = requisition.Status,
                    workflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId,
                    workflowOutcome = workflowResult.Outcome.ToString(),
                    budgetControl = approvalBudgetReadiness,
                    budgetRelease
                }
            });
        }
        catch (ProcurementRequisitionAuthorityAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_AUTHORITY_APPROVAL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementAccessAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, Problem(
                "PR_APPROVAL_CAPABILITY_REQUIRED", ex.Message, StatusCodes.Status403Forbidden));
        }
        catch (ProcurementRequisitionAuthorityNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionAuthorityBlockedException ex)
        {
            var status = ex.Readiness.DecisionCode.StartsWith("SOD_", StringComparison.OrdinalIgnoreCase) ||
                         ex.Readiness.DecisionCode.StartsWith("SOD-", StringComparison.OrdinalIgnoreCase)
                ? 403
                : 409;
            var problem = Problem(ex.Readiness.DecisionCode, ex.Message, status);
            problem.Extensions["authorityReadiness"] = ex.Readiness;
            return StatusCode(status, problem);
        }
        catch (ProcurementRequisitionAuthorityConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionBudgetAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_BUDGET_RELEASE_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionBudgetNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionBudgetConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionBudgetValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase requisition {RequisitionId}", id);
            // Preserve the central exception-handling boundary so the caller receives
            // safe ProblemDetails while support retains the complete incident record.
            throw;
        }
    }

    /// <summary>
    /// Submits a purchase requisition for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitPurchaseRequisition(Guid id)
    {
        try
        {
            if (!_currentUserProvider.IsAuthenticated)
            {
                return Unauthorized();
            }

            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound(Problem("PR_NOT_FOUND", "The purchase requisition was not found in the current tenant.", 404));
            }

            if (requisition.Status != "Draft")
            {
                if (string.Equals(requisition.Status, "Pending Approval", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(requisition.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
                {
                    var retryReadiness = await _budgetControlService.GetReadinessAsync(
                        requisition.Id, HttpContext.RequestAborted);
                    return Ok(new
                    {
                        success = true,
                        idempotent = true,
                        message = "The purchase requisition was already submitted to its approval workflow.",
                        data = new { id = requisition.Id, status = requisition.Status, budgetControl = retryReadiness }
                    });
                }
                return Conflict(Problem("PR_NOT_DRAFT", $"Purchase requisition cannot be submitted in current status: {requisition.Status}.", 409));
            }

            if (requisition.TotalAmount <= 0 || requisition.Items.Any(item =>
                    item.Quantity <= 0 || item.EstimatedUnitPrice <= 0 || item.LineTotal <= 0))
            {
                return UnprocessableEntity(Problem(
                    "PR_ESTIMATE_REQUIRED",
                    "Every requisition line requires a governed positive estimated cost before submission.",
                    422));
            }

            var submissionReadiness = await _submissionControlService.EnforceAsync(
                requisition, CorrelationId, HttpContext.RequestAborted);

            var budgetReadiness = await _budgetControlService.GetReadinessAsync(
                requisition.Id, HttpContext.RequestAborted);
            if (!budgetReadiness.CanReserve)
            {
                var problem = Problem(budgetReadiness.DecisionCode, budgetReadiness.Message, 422);
                problem.Extensions["budgetReadiness"] = budgetReadiness;
                return UnprocessableEntity(problem);
            }

            WorkflowIntegrationResult? workflowResult = null;
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(HttpContext.RequestAborted);
                try
                {
                    workflowResult = await _workflowIntegrationService.SubmitAsync(
                        "PurchaseRequisition", id);
                    if (!workflowResult.ExecutionResult.Success)
                        throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Purchase requisition submission failed.");
                    requisition.ApprovalRequired = workflowResult.ApprovalRequired;
                    var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("PurchaseRequisition");
                    statusAdapter.ApplySubmitOutcome(requisition, workflowResult, _currentUserProvider.UserId);
                    await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
                    await _unitOfWork.CommitAsync(HttpContext.RequestAborted);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(HttpContext.RequestAborted);
                    throw;
                }
            }, HttpContext.RequestAborted);

            if (workflowResult is null)
                return Conflict(Problem("PR_WORKFLOW_RESULT_MISSING", "The workflow did not return a submission outcome.", 409));

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = requisition.TenantId,
                    EntityType = "PurchaseRequisition",
                    Activity = "Submitted",
                    Audience = "Internal",
                    EntityId = requisition.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>
                    {
                        ["PurchaseRequisitionId"] = requisition.Id,
                        ["RequisitionNumber"] = requisition.RequisitionNumber ?? string.Empty,
                        ["Status"] = requisition.Status ?? string.Empty,
                        ["WorkflowInstanceId"] = workflowResult.ExecutionResult.WorkflowInstanceId?.ToString() ?? string.Empty,
                        ["WorkflowOutcome"] = workflowResult.Outcome.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish PurchaseRequisition.Submitted entity activity event for requisition {RequisitionId}", requisition.Id);
            }

            return Ok(new
            {
                success = true,
                message = workflowResult.ApprovalRequired ? "Purchase requisition submitted for approval" : "Purchase requisition finalized; approval is not required",
                data = new
                {
                    id = requisition.Id,
                    status = requisition.Status,
                    workflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId,
                    approvalRequired = workflowResult.ApprovalRequired,
                    workflowOutcome = workflowResult.Outcome.ToString(),
                    submissionControl = submissionReadiness,
                    budgetControl = budgetReadiness
                }
            });
        }
        catch (ProcurementRequisitionSubmissionAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SUBMISSION_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSubmissionNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionSubmissionBlockedException ex)
        {
            var problem = Problem(ex.Readiness.DecisionCode, ex.Message, 422);
            problem.Extensions["submissionReadiness"] = ex.Readiness;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementControlEventConflictException ex)
        {
            return Conflict(Problem("PR_SUBMISSION_AUDIT_CONFLICT", ex.Message, 409));
        }
        catch (ProcurementRequisitionAuthorityAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_AUTHORITY_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionAuthorityNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionAuthorityBlockedException ex)
        {
            var problem = Problem(ex.Readiness.DecisionCode, ex.Message, 422);
            problem.Extensions["authorityReadiness"] = ex.Readiness;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementRequisitionAuthorityConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionBudgetAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_BUDGET_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionBudgetNotFoundException ex)
        {
            return NotFound(Problem(ex.Code, ex.Message, 404));
        }
        catch (ProcurementRequisitionBudgetConflictException ex)
        {
            return Conflict(Problem(ex.Code, ex.Message, 409));
        }
        catch (ProcurementRequisitionBudgetValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Problem("PR_WORKFLOW_SUBMISSION_FAILED", ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting purchase requisition {RequisitionId}", id);
            return StatusCode(500, "An error occurred while submitting the purchase requisition");
        }
    }

    /// <summary>
    /// Gets purchase requisitions by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<List<PurchaseRequisitionSummaryDto>>> GetPurchaseRequisitionsByStatus(string status)
    {
        try
        {
            var requisitions = await _purchaseRequisitionRepository.GetRequisitionsByStatus(status);

            var requisitionDtos = requisitions.Select(MapSummary).ToList();

            await PopulateCurrentStepNamesAsync(requisitionDtos);
            return Ok(requisitionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase requisitions by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving purchase requisitions");
        }
    }

    /// <summary>
    /// Gets purchase requisitions by priority
    /// </summary>
    [HttpGet("by-priority/{priority}")]
    public async Task<ActionResult<List<PurchaseRequisitionSummaryDto>>> GetPurchaseRequisitionsByPriority(string priority)
    {
        try
        {
            var requisitions = await _purchaseRequisitionRepository.GetRequisitionsByPriority(priority);

            var requisitionDtos = requisitions.Select(MapSummary).ToList();

            await PopulateCurrentStepNamesAsync(requisitionDtos);
            return Ok(requisitionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase requisitions by priority {Priority}", priority);
            return StatusCode(500, "An error occurred while retrieving purchase requisitions");
        }
    }

    /// <summary>
    /// Gets purchase requisitions by department
    /// </summary>
    [HttpGet("by-department/{department}")]
    public async Task<ActionResult<List<PurchaseRequisitionSummaryDto>>> GetPurchaseRequisitionsByDepartment(string department)
    {
        try
        {
            var requisitions = await _purchaseRequisitionRepository.GetRequisitionsByDepartment(department);

            var requisitionDtos = requisitions.Select(MapSummary).ToList();

            await PopulateCurrentStepNamesAsync(requisitionDtos);
            return Ok(requisitionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase requisitions by department {Department}", department);
            return StatusCode(500, "An error occurred while retrieving purchase requisitions");
        }
    }

    /// <summary>
    /// Gets pending approval purchase requisitions
    /// </summary>
    [HttpGet("pending-approval")]
    public async Task<ActionResult<List<PurchaseRequisitionSummaryDto>>> GetPendingApprovalRequisitions()
    {
        try
        {
            var requisitions = await _purchaseRequisitionRepository.GetPendingApprovalRequisitions();

            var requisitionDtos = requisitions.Select(MapSummary).ToList();

            await PopulateCurrentStepNamesAsync(requisitionDtos);
            return Ok(requisitionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approval requisitions");
            return StatusCode(500, "An error occurred while retrieving pending approval requisitions");
        }
    }

    /// <summary>
    /// Gets approved purchase requisitions that haven't been converted to purchase orders
    /// </summary>
    [HttpGet("approved-pending-po")]
    public async Task<ActionResult<List<PurchaseRequisitionSummaryDto>>> GetApprovedPendingPORequisitions()
    {
        try
        {
            // Use GetRequisitionsByStatusAsync since GetApprovedPendingPORequisitions doesn't exist in the interface
            var requisitions = await _purchaseRequisitionRepository.GetRequisitionsByStatusAsync("Approved");

            var requisitionDtos = requisitions.Select(r => new PurchaseRequisitionSummaryDto
            {
                Id = r.Id,
                RequisitionNumber = r.RequisitionNumber,
                RequisitionDate = r.RequisitionDate,
                RequestedByName = r.RequestedBy?.FirstName + " " + r.RequestedBy?.LastName,
                RequiredDate = r.RequiredDate,
                Status = r.Status,
                ApprovalRequired = r.ApprovalRequired,
                Priority = r.Priority,
                Department = r.Department,
                TotalAmount = r.TotalAmount,
                ItemCount = r.Items?.Count ?? 0
            }).ToList();

            await PopulateCurrentStepNamesAsync(requisitionDtos);
            return Ok(requisitionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approved pending PO requisitions");
            return StatusCode(500, "An error occurred while retrieving approved pending PO requisitions");
        }
    }

    /// <summary>
    /// Converts a purchase requisition to purchase order
    /// </summary>
    [HttpPost("{id}/convert-to-po")]
    public async Task<ActionResult<CreatePurchaseOrderDto>> ConvertToPurchaseOrder(Guid id)
    {
        try
        {
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            if (requisition.Status != "Approved")
            {
                return BadRequest($"Purchase requisition must be approved to convert to purchase order. Current status: {requisition.Status}");
            }

            return Conflict(new
            {
                code = "PO_APPROVED_SOURCE_REQUIRED",
                message = "An approved requisition is demand authority, not a sourcing award. Complete the requisition's governed sourcing/award, contract, framework call-off, or approved-exception path before creating a purchase order.",
                purchaseRequisitionId = requisition.Id,
                requisition.RequisitionNumber,
                sourceOptionsUrl =
                    $"/api/PurchaseOrders/source-options?purchaseRequisitionId={requisition.Id}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting requisition to purchase order {RequisitionId}", id);
            return StatusCode(500, "An error occurred while converting the requisition to purchase order");
        }
    }

    /// <summary>
    /// Suggest suppliers for an RFQ based on the requisition's preferred suppliers and item-supplier mappings.
    /// Returns suppliers ordered by relevance (preferred suppliers first).
    /// </summary>
    [HttpGet("{id}/suggested-suppliers")]
    public async Task<ActionResult<List<SuggestedSupplierDto>>> GetSuggestedSuppliers(Guid id)
    {
        try
        {
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
                return NotFound($"Purchase requisition with ID {id} not found");

            var items = (await _purchaseRequisitionItemRepository.GetItemsByRequisitionIdAsync(id)).ToList();

            // Preferred suppliers from PR items.
            var preferredSupplierIds = items
                .Where(i => i.PreferredBusinessPartnerId.HasValue && i.PreferredBusinessPartnerId.Value != Guid.Empty)
                .Select(i => i.PreferredBusinessPartnerId!.Value)
                .ToList();

            // Item-supplier mappings for PR inventory items.
            var inventoryItemIds = items
                .Where(i => i.InventoryItemId.HasValue && i.InventoryItemId.Value != Guid.Empty)
                .Select(i => i.InventoryItemId!.Value)
                .Distinct()
                .ToList();

            var itemSupplierRepo = _unitOfWork.Repository<ItemSupplier>();
            var itemSuppliers = inventoryItemIds.Count == 0
                ? new List<ItemSupplier>()
                : (await itemSupplierRepo.FindAsync(x => inventoryItemIds.Contains(x.InventoryItemId) && !x.IsDeleted)).ToList();

            // Score suppliers: preferred suppliers get priority; item matches are next.
            var counts = new Dictionary<Guid, SuggestedSupplierDto>();

            foreach (var prefId in preferredSupplierIds)
            {
                if (!counts.TryGetValue(prefId, out var dto))
                {
                    dto = new SuggestedSupplierDto { SupplierId = prefId };
                    counts[prefId] = dto;
                }
                dto.PreferredItemCount++;
            }

            // For each PR item, count which suppliers can supply it.
            var supplierIdsByItem = itemSuppliers
                .GroupBy(s => s.InventoryItemId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.SupplierId).Distinct().ToList());

            foreach (var prItem in items)
            {
                if (!prItem.InventoryItemId.HasValue || prItem.InventoryItemId.Value == Guid.Empty)
                    continue;

                if (!supplierIdsByItem.TryGetValue(prItem.InventoryItemId.Value, out var supplierIds))
                    continue;

                foreach (var supplierId in supplierIds)
                {
                    if (!counts.TryGetValue(supplierId, out var dto))
                    {
                        dto = new SuggestedSupplierDto { SupplierId = supplierId };
                        counts[supplierId] = dto;
                    }
                    dto.ItemMatchCount++;
                }
            }

            var ordered = counts.Values
                .OrderByDescending(x => x.PreferredItemCount)
                .ThenByDescending(x => x.ItemMatchCount)
                .ToList();

            return Ok(ordered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggested suppliers for requisition {RequisitionId}", id);
            return StatusCode(500, "An error occurred while generating suggested suppliers");
        }
    }

    /// <summary>
    /// Creates a Draft RFQ from an approved purchase requisition.
    /// The RFQ can then be edited (suppliers, deadline) and sent to notify suppliers.
    /// </summary>
    [HttpPost("{id}/create-rfq")]
    public async Task<ActionResult<CreateRfqFromPurchaseRequisitionResponseDto>> CreateRfqFromPurchaseRequisition(Guid id)
    {
        try
        {
            var rfq = await _rfqService.CreateRfqFromPurchaseRequisitionAsync(id);

            return Ok(new CreateRfqFromPurchaseRequisitionResponseDto
            {
                RfqId = rfq.Id,
                RfqNumber = rfq.RfqNumber
            });
        }
        catch (ProcurementRequisitionSourcingBlockedException ex)
        {
            var problem = Problem(ex.Readiness.DecisionCode, ex.Message, 422);
            problem.Extensions["readiness"] = ex.Readiness;
            return UnprocessableEntity(problem);
        }
        catch (ProcurementRequisitionSourcingAuthorizationException ex)
        {
            return StatusCode(403, Problem("PR_SOURCING_CONTROL_FORBIDDEN", ex.Message, 403));
        }
        catch (ProcurementRequisitionSourcingValidationException ex)
        {
            return UnprocessableEntity(Problem(ex.Code, ex.Message, 422));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RFQ from requisition {RequisitionId}", id);
            return StatusCode(500, "An error occurred while creating the RFQ");
        }
    }

    #region Private Helper Methods

    private static PurchaseRequisitionSummaryDto MapSummary(PurchaseRequisition requisition) => new()
    {
        Id = requisition.Id,
        RequisitionNumber = requisition.RequisitionNumber,
        RequisitionDate = requisition.RequisitionDate,
        RequestedByName = requisition.RequestedBy?.FirstName + " " + requisition.RequestedBy?.LastName,
        RequiredDate = requisition.RequiredDate,
        Status = requisition.Status,
        ApprovalRequired = requisition.ApprovalRequired,
        Priority = requisition.Priority,
        Department = requisition.Department,
        OrganizationUnitId = requisition.OrganizationUnitId,
        TotalAmount = requisition.TotalAmount,
        Currency = requisition.Currency,
        ItemCount = requisition.Items?.Count ?? 0,
        SourcePlanNumber = requisition.SourcePlanNumber,
        SourcePlanItemId = requisition.SourcePlanItemId,
        SourcePlanItemIds = requisition.Items?
            .Where(item => !item.IsDeleted && item.SourcePlanItemId.HasValue)
            .Select(item => item.SourcePlanItemId!.Value).Distinct().ToList() ?? [],
        SourcePlanItemDescription = requisition.SourcePlanItemDescription,
        BudgetCode = requisition.BudgetCode,
        ProcurementCategory = requisition.ProcurementCategory,
        ProjectCode = requisition.ProjectCode,
        RequisitionType = requisition.RequisitionType,
        SpecificationTemplateReference = SpecificationReference(requisition),
        ApprovedExceptionReference = requisition.ExceptionApprovalReference
    };

    private async Task<PurchaseRequisitionDetailDto> GetPurchaseRequisitionDetailDto(Guid requisitionId)
    {
        var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(requisitionId);
        if (requisition == null)
        {
            return null!;
        }

        var items = await _purchaseRequisitionItemRepository.GetItemsByRequisitionIdAsync(requisitionId);

        return new PurchaseRequisitionDetailDto
        {
            Id = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            RequisitionDate = requisition.RequisitionDate,
            RequestedByName = requisition.RequestedBy?.FirstName + " " + requisition.RequestedBy?.LastName,
            RequiredDate = requisition.RequiredDate,
            Status = requisition.Status,
            ApprovalRequired = requisition.ApprovalRequired,
            Priority = requisition.Priority,
            Department = requisition.Department,
            OrganizationUnitId = requisition.OrganizationUnitId,
            CostCenter = requisition.CostCenter,
            Justification = requisition.Justification,
            Notes = requisition.Notes,
            ApprovedByName = requisition.ApprovedBy?.FirstName + " " + requisition.ApprovedBy?.LastName,
            ApprovedAt = requisition.ApprovedAt,
            RejectionReason = requisition.RejectionReason,
            TotalAmount = requisition.TotalAmount,
            Currency = requisition.Currency,
            ItemCount = items.Count(),
            SourcePlanNumber = requisition.SourcePlanNumber,
            SourcePlanItemId = requisition.SourcePlanItemId,
            SourcePlanItemIds = items.Where(item => item.SourcePlanItemId.HasValue)
                .Select(item => item.SourcePlanItemId!.Value).Distinct().ToList(),
            SourcePlanItemDescription = requisition.SourcePlanItemDescription,
            BudgetCode = requisition.BudgetCode,
            ProcurementCategory = requisition.ProcurementCategory,
            ProjectCode = requisition.ProjectCode,
            RequisitionType = requisition.RequisitionType,
            SpecificationTemplateReference = SpecificationReference(requisition),
            ApprovedExceptionReference = requisition.ExceptionApprovalReference,
            Linkage = _linkageService.Map(requisition),
            RowVersion = Convert.ToBase64String(requisition.RowVersion),
            Items = items.Select(item => new PurchaseRequisitionItemDto
            {
                Id = item.Id,
                RequisitionId = item.RequisitionId,
                InventoryItemId = item.InventoryItemId,
                SourcePlanItemId = item.SourcePlanItemId,
                ItemDescription = item.ItemDescription,
                Quantity = item.Quantity,
                UnitOfMeasure = item.UnitOfMeasure,
                EstimatedUnitPrice = item.EstimatedUnitPrice,
                LineTotal = item.LineTotal,
                RequiredDate = item.RequiredDate,
                PreferredSupplierId = item.PreferredBusinessPartnerId,
                PreferredSupplierName = item.PreferredBusinessPartner?.PartnerName,
                Notes = item.Notes,
                Specifications = item.Specifications,
                Status = item.Status,
                PurchaseOrderId = item.PurchaseOrderId,
                PurchaseOrderNumber = item.PurchaseOrder?.OrderNumber,
                ItemCode = item.InventoryItem?.ItemCode,
                ItemName = item.InventoryItem?.Name
            }).ToList()
        };
    }

    private string CorrelationId => string.IsNullOrWhiteSpace(HttpContext?.TraceIdentifier)
        ? Guid.NewGuid().ToString("N")
        : HttpContext.TraceIdentifier;

    private ProblemDetails Problem(string code, string detail, int status)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Instance = HttpContext?.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationId;
        return problem;
    }

    private static string? ValidateDraftContract(CreatePurchaseRequisitionDto request)
    {
        if (request.Items is null || request.Items.Count == 0) return "At least one requisition item is required.";
        if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.ItemDescription))) return "Every requisition item requires a description.";
        if (request.Items.Any(item => item.Quantity <= 0)) return "Every requisition item quantity must be greater than zero.";
        if (request.Items.Any(item => item.EstimatedUnitPrice < 0)) return "Estimated unit prices cannot be negative.";
        if (string.IsNullOrWhiteSpace(request.Priority)) return "Priority is required.";
        if (!string.IsNullOrWhiteSpace(request.Currency) && request.Currency.Trim().Length != 3)
            return "Currency must be a three-letter Finance currency code.";
        if ((!request.OrganizationUnitId.HasValue || request.OrganizationUnitId.Value == Guid.Empty) &&
            (!request.Linkage.SourcePlanItemId.HasValue || request.Linkage.SourcePlanItemId.Value == Guid.Empty))
            return "Select an active HR organization unit.";
        return null;
    }

    private async Task<OrganizationUnit> ResolveOrganizationUnitAsync(
        Guid? organizationUnitId,
        Guid? sourcePlanItemId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (sourcePlanItemId.HasValue && sourcePlanItemId.Value != Guid.Empty)
        {
            var sourcePlanItem = await _unitOfWork.Repository<ProcurementPlanItem>()
                .GetQueryable(item => item.Id == sourcePlanItemId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Include(item => item.ProcurementPlan)
                .ThenInclude(plan => plan.OrganizationUnit)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (sourcePlanItem is null || sourcePlanItem.ProcurementPlan.IsDeleted ||
                sourcePlanItem.ProcurementPlan.OrganizationUnit is null ||
                sourcePlanItem.ProcurementPlan.OrganizationUnit.IsDeleted || !sourcePlanItem.ProcurementPlan.OrganizationUnit.IsActive)
                throw new ProcurementRequisitionLinkageValidationException(
                    "PR_PLAN_ORGANIZATION_UNIT_INVALID",
                    "The selected plan item does not have an active HR organization unit in the current tenant.");
            if (organizationUnitId.HasValue && organizationUnitId.Value != Guid.Empty &&
                organizationUnitId.Value != sourcePlanItem.ProcurementPlan.OrganizationUnitId)
                throw new ProcurementRequisitionLinkageValidationException(
                    "PR_PLAN_ORGANIZATION_UNIT_MISMATCH",
                    "The posted organization unit does not match the selected procurement plan item.");
            return sourcePlanItem.ProcurementPlan.OrganizationUnit;
        }

        if (!organizationUnitId.HasValue || organizationUnitId.Value == Guid.Empty)
            throw new ProcurementRequisitionLinkageValidationException(
                "PR_ORGANIZATION_UNIT_REQUIRED",
                "Select an active HR organization unit before saving the requisition.");

        var organizationUnit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(organizationUnitId.Value);
        if (organizationUnit is null || organizationUnit.TenantId != tenantId || organizationUnit.IsDeleted || !organizationUnit.IsActive)
            throw new ProcurementRequisitionLinkageValidationException(
                "PR_ORGANIZATION_UNIT_INVALID",
                "The selected HR organization unit is not available in the current tenant.");

        return organizationUnit;
    }

    private async Task<string> ResolveRequisitionCurrencyAsync(
        string? requestedCurrency,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var currencies = _unitOfWork.Repository<ErpSystem.Core.Entities.Finance.Currency>()
            .GetQueryable(currency =>
                currency.TenantId == tenantId &&
                currency.IsActive &&
                !currency.IsDeleted);

        if (string.IsNullOrWhiteSpace(requestedCurrency))
        {
            var baseCurrency = await currencies
                .AsNoTracking()
                .Where(currency => currency.IsBaseCurrency)
                .Select(currency => currency.CurrencyCode)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(baseCurrency))
                throw new ProcurementRequisitionLinkageValidationException(
                    "PR_BASE_CURRENCY_NOT_CONFIGURED",
                    "Finance must configure an active tenant base currency before a purchase requisition can be created.");
            return baseCurrency.Trim().ToUpperInvariant();
        }

        var normalized = requestedCurrency.Trim().ToUpperInvariant();
        var activeCurrency = await currencies
            .AsNoTracking()
            .Where(currency => currency.CurrencyCode == normalized)
            .Select(currency => currency.CurrencyCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(activeCurrency))
            throw new ProcurementRequisitionLinkageValidationException(
                "PR_CURRENCY_NOT_ACTIVE",
                $"Currency {normalized} is not active in Finance for this tenant.");
        return activeCurrency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// A linked approved-plan estimate is authoritative for its requisition line.
    /// Otherwise inventory-backed demand is valued from the controlled item master;
    /// non-inventory demand retains its captured governed estimate. Zero or negative
    /// estimates are rejected before the requisition can enter approval workflow.
    /// </summary>
    private async Task ApplyAuthoritativeInventoryPricingAsync(
        IEnumerable<CreatePurchaseRequisitionItemDto> items,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var requestItems = items.ToList();
        var planPricedLines = new HashSet<CreatePurchaseRequisitionItemDto>();
        var sourcePlanItemIds = requestItems.Where(item => item.SourcePlanItemId.HasValue)
            .Select(item => item.SourcePlanItemId!.Value).Distinct().ToList();
        var planItems = sourcePlanItemIds.Count == 0
            ? new Dictionary<Guid, ProcurementPlanItem>()
            : await _unitOfWork.Repository<ProcurementPlanItem>()
                .GetQueryable(item => sourcePlanItemIds.Contains(item.Id) &&
                    item.TenantId == tenantId && !item.IsDeleted)
                .AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        if (planItems.Count != sourcePlanItemIds.Count)
            throw new ProcurementRequisitionLinkageNotFoundException(
                "PLAN_ITEM_NOT_FOUND", "One or more requisition lines reference an unavailable procurement-plan item.");

        foreach (var line in requestItems.Where(item => item.SourcePlanItemId.HasValue))
        {
            var planItem = planItems[line.SourcePlanItemId!.Value];
            if (planItem.InventoryItemId.HasValue && line.InventoryItemId.HasValue &&
                planItem.InventoryItemId.Value != line.InventoryItemId.Value)
                throw new ProcurementRequisitionLinkageValidationException(
                    "PLAN_ITEM_LINE_MISMATCH",
                    $"Requisition line {line.ItemDescription} does not match its linked procurement-plan item.");
            var plannedUnitEstimate = planItem.EstimatedUnitPrice > 0
                ? planItem.EstimatedUnitPrice
                : planItem.EstimatedQuantity > 0 && planItem.EstimatedTotalCost > 0
                    ? planItem.EstimatedTotalCost / planItem.EstimatedQuantity
                    : 0;
            if (plannedUnitEstimate > 0)
            {
                line.EstimatedUnitPrice = plannedUnitEstimate;
                planPricedLines.Add(line);
            }
        }

        var inventoryItems = _unitOfWork.Repository<InventoryItem>();

        foreach (var requestItem in requestItems.Where(item =>
                     !planPricedLines.Contains(item) &&
                     item.InventoryItemId.HasValue && item.InventoryItemId.Value != Guid.Empty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inventoryItem = await inventoryItems.GetByIdAsync(requestItem.InventoryItemId!.Value);
            if (inventoryItem is null || inventoryItem.TenantId != tenantId || inventoryItem.IsDeleted || inventoryItem.Status != ItemStatus.Active)
            {
                throw new ProcurementRequisitionLinkageValidationException(
                    "PR_INVENTORY_ITEM_INVALID",
                    "A requisition item must reference an active inventory item in the current tenant.");
            }

            requestItem.EstimatedUnitPrice = inventoryItem.LastPurchaseCost > 0
                ? inventoryItem.LastPurchaseCost
                : inventoryItem.StandardCost > 0
                    ? inventoryItem.StandardCost
                    : inventoryItem.AverageCost;
        }

        if (requestItems.Any(item => item.EstimatedUnitPrice <= 0))
        {
            throw new ProcurementRequisitionLinkageValidationException(
                "PR_ESTIMATE_REQUIRED",
                "Every requisition line requires a governed positive estimate from its linked plan item, controlled item master, or captured requisition estimate.");
        }
    }

    private static bool TryDecodeRowVersion(string value, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static PurchaseRequisitionItem CreateItem(
        Guid tenantId,
        Guid requisitionId,
        CreatePurchaseRequisitionItemDto itemDto)
    {
        var now = DateTime.UtcNow;
        return new PurchaseRequisitionItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequisitionId = requisitionId,
            InventoryItemId = itemDto.InventoryItemId,
            SourcePlanItemId = itemDto.SourcePlanItemId,
            ItemDescription = itemDto.ItemDescription.Trim(),
            Quantity = itemDto.Quantity,
            UnitOfMeasure = string.IsNullOrWhiteSpace(itemDto.UnitOfMeasure) ? "EA" : itemDto.UnitOfMeasure.Trim(),
            EstimatedUnitPrice = itemDto.EstimatedUnitPrice,
            LineTotal = itemDto.Quantity * itemDto.EstimatedUnitPrice,
            RequiredDate = itemDto.RequiredDate,
            PreferredBusinessPartnerId = itemDto.PreferredSupplierId,
            Notes = TrimOrNull(itemDto.Notes, 1000),
            Specifications = TrimOrNull(itemDto.Specifications, 1000),
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void NormalizePlanItemLineage(CreatePurchaseRequisitionDto request)
    {
        var primaryId = request.Linkage.SourcePlanItemId is { } value && value != Guid.Empty
            ? value
            : (Guid?)null;
        var explicitLineIds = request.Items.Where(item => item.SourcePlanItemId.HasValue &&
                item.SourcePlanItemId.Value != Guid.Empty)
            .Select(item => item.SourcePlanItemId!.Value).Distinct().ToList();

        if (explicitLineIds.Count == 0 && primaryId.HasValue)
        {
            foreach (var item in request.Items)
                item.SourcePlanItemId = primaryId.Value;
            explicitLineIds.Add(primaryId.Value);
        }
        else if (explicitLineIds.Count > 0 && request.Items.Any(item => !item.SourcePlanItemId.HasValue ||
                     item.SourcePlanItemId.Value == Guid.Empty))
        {
            throw new ProcurementRequisitionLinkageValidationException(
                "PR_PLAN_LINEAGE_INCOMPLETE",
                "Every line in a plan-linked requisition must retain its exact procurement-plan item reference.");
        }

        request.Linkage.SourcePlanItemIds = explicitLineIds;
        request.Linkage.SourcePlanItemId = primaryId.HasValue && explicitLineIds.Contains(primaryId.Value)
            ? primaryId.Value
            : explicitLineIds.Count > 0 ? explicitLineIds[0] : null;
    }

    private static string? SpecificationReference(PurchaseRequisition requisition) =>
        string.IsNullOrWhiteSpace(requisition.SpecificationTemplateCode)
            ? null
            : $"{requisition.SpecificationTemplateCode}/v{requisition.SpecificationTemplateVersion}";

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private async Task PopulateCurrentStepNamesAsync(IEnumerable<PurchaseRequisitionSummaryDto> requisitions)
    {
        var pending = requisitions
            .Where(d => d.Status == "Pending Approval" || d.Status == "Submitted")
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        await Task.WhenAll(pending.Select(async dto =>
        {
            try
            {
                var currentStep = await _workflowService.GetCurrentWorkflowStepAsync("PurchaseRequisition", dto.Id);
                dto.CurrentWorkflowStepName = currentStep?.StepName;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve current workflow step for PR {RequisitionId}", dto.Id);
            }
        }));
    }

    #endregion
}
