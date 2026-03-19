using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        _appEventBus = appEventBus;
        _logger = logger;
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
        [FromQuery] string? department = null)
    {
        try
        {
            var requisitions = await _purchaseRequisitionRepository.GetRequisitionsAsync(
                page, pageSize, search, status, priority, startDate, endDate, department);

            var requisitionDtos = requisitions.Items.Select(r => new PurchaseRequisitionSummaryDto
            {
                Id = r.Id,
                RequisitionNumber = r.RequisitionNumber,
                RequisitionDate = r.RequisitionDate,
                RequestedByName = r.RequestedBy?.FirstName + " " + r.RequestedBy?.LastName,
                RequiredDate = r.RequiredDate,
                Status = r.Status,
                Priority = r.Priority,
                Department = r.Department,
                TotalAmount = r.TotalAmount,
                ItemCount = r.Items?.Count ?? 0
            }).ToList();

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
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            var items = await _purchaseRequisitionItemRepository.GetItemsByRequisitionIdAsync(id);

            var requisitionDto = new PurchaseRequisitionDetailDto
            {
                Id = requisition.Id,
                RequisitionNumber = requisition.RequisitionNumber,
                RequisitionDate = requisition.RequisitionDate,
                RequestedByName = requisition.RequestedBy?.FirstName + " " + requisition.RequestedBy?.LastName,
                RequiredDate = requisition.RequiredDate,
                Status = requisition.Status,
                Priority = requisition.Priority,
                Department = requisition.Department,
                CostCenter = requisition.CostCenter,
                Justification = requisition.Justification,
                Notes = requisition.Notes,
                ApprovedByName = requisition.ApprovedBy?.FirstName + " " + requisition.ApprovedBy?.LastName,
                ApprovedAt = requisition.ApprovedAt,
                RejectionReason = requisition.RejectionReason,
                TotalAmount = requisition.TotalAmount,
                ItemCount = items.Count(),
                Items = items.Select(item => new PurchaseRequisitionItemDto
                {
                    Id = item.Id,
                    RequisitionId = item.RequisitionId,
                    InventoryItemId = item.InventoryItemId,
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
        [FromBody] CreatePurchaseRequisitionDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get tenant ID from context
            var tenantId = _tenantContext.GetCurrentTenantId();

            // Generate requisition number
            var requisitionNumber = await _purchaseRequisitionRepository.GenerateRequisitionNumberAsync();

            // Calculate total amount
            var totalAmount = createDto.Items.Sum(item => item.Quantity * item.EstimatedUnitPrice);

            var requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequisitionNumber = requisitionNumber,
                RequisitionDate = DateTime.UtcNow,
                RequestedById = createDto.RequestedById,
                RequiredDate = createDto.RequiredDate,
                Status = "Draft",
                Priority = createDto.Priority,
                Department = createDto.Department,
                CostCenter = createDto.CostCenter,
                Justification = createDto.Justification,
                Notes = createDto.Notes,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _purchaseRequisitionRepository.CreateRequisitionAsync(requisition);

            // Create requisition items
            foreach (var itemDto in createDto.Items)
            {
                var item = new PurchaseRequisitionItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RequisitionId = requisition.Id,
                    InventoryItemId = itemDto.InventoryItemId,
                    ItemDescription = itemDto.ItemDescription,
                    Quantity = itemDto.Quantity,
                    UnitOfMeasure = itemDto.UnitOfMeasure ?? "EA",
                    EstimatedUnitPrice = itemDto.EstimatedUnitPrice,
                    LineTotal = itemDto.Quantity * itemDto.EstimatedUnitPrice,
                    RequiredDate = itemDto.RequiredDate,
                    PreferredBusinessPartnerId = itemDto.PreferredSupplierId,
                    Notes = itemDto.Notes,
                    Specifications = itemDto.Specifications,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _purchaseRequisitionItemRepository.CreateItemAsync(item);
            }

            // CRITICAL: Save changes to database
            await _unitOfWork.SaveChangesAsync();

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var triggeredBy = _currentUserProvider.IsAuthenticated ? _currentUserProvider.UserId : (Guid?)null;
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = "PurchaseRequisition",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = requisition.Id,
                    TriggeredByUserId = triggeredBy,
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

            // Return the created requisition
            var createdRequisition = await GetPurchaseRequisitionDetailDto(requisition.Id);
            return CreatedAtAction(nameof(GetPurchaseRequisition), new { id = requisition.Id }, createdRequisition);
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

            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            await _purchaseRequisitionRepository.UpdateStatusAsync(id, statusDto.Status);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
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

            if (requisition.Status != "Pending Approval" && requisition.Status != "Draft")
            {
                return BadRequest($"Purchase requisition cannot be approved in current status: {requisition.Status}");
            }

            var userId = _currentUserProvider.UserId;
            if (userId == Guid.Empty)
            {
                return Unauthorized("User identifier claim is missing or invalid");
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync("PurchaseRequisition", id, userId);
            if (!canApprove)
            {
                return StatusCode(403, "You are not assigned as an approver for the current workflow step");
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

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                "PurchaseRequisition",
                id,
                userId,
                action,
                comments);

            var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("PurchaseRequisition");
            statusAdapter.ApplyApprovalOutcome(requisition, workflowResult.Outcome, userId, approvalDto.RejectionReason);

            await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
            await _unitOfWork.SaveChangesAsync();

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
                    workflowOutcome = workflowResult.Outcome.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving purchase requisition {RequisitionId}", id);
            return StatusCode(500, "An error occurred while approving the purchase requisition");
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
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            if (requisition.Status != "Draft")
            {
                return BadRequest($"Purchase requisition cannot be submitted in current status: {requisition.Status}");
            }

            WorkflowIntegrationResult workflowResult;
            try
            {
                workflowResult = await _workflowIntegrationService.SubmitAsync("PurchaseRequisition", id);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("PurchaseRequisition");
            statusAdapter.ApplySubmitOutcome(requisition, workflowResult.Outcome, _currentUserProvider.UserId);

            await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
            await _unitOfWork.SaveChangesAsync();

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
                message = "Purchase requisition submitted for approval",
                data = new
                {
                    id = requisition.Id,
                    status = requisition.Status,
                    workflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId,
                    workflowOutcome = workflowResult.Outcome.ToString()
                }
            });
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

            var requisitionDtos = requisitions.Select(r => new PurchaseRequisitionSummaryDto
            {
                Id = r.Id,
                RequisitionNumber = r.RequisitionNumber,
                RequisitionDate = r.RequisitionDate,
                RequestedByName = r.RequestedBy?.FirstName + " " + r.RequestedBy?.LastName,
                RequiredDate = r.RequiredDate,
                Status = r.Status,
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

            var requisitionDtos = requisitions.Select(r => new PurchaseRequisitionSummaryDto
            {
                Id = r.Id,
                RequisitionNumber = r.RequisitionNumber,
                RequisitionDate = r.RequisitionDate,
                RequestedByName = r.RequestedBy?.FirstName + " " + r.RequestedBy?.LastName,
                RequiredDate = r.RequiredDate,
                Status = r.Status,
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

            var requisitionDtos = requisitions.Select(r => new PurchaseRequisitionSummaryDto
            {
                Id = r.Id,
                RequisitionNumber = r.RequisitionNumber,
                RequisitionDate = r.RequisitionDate,
                RequestedByName = r.RequestedBy?.FirstName + " " + r.RequestedBy?.LastName,
                RequiredDate = r.RequiredDate,
                Status = r.Status,
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

            var requisitionDtos = requisitions.Select(r => new PurchaseRequisitionSummaryDto
            {
                Id = r.Id,
                RequisitionNumber = r.RequisitionNumber,
                RequisitionDate = r.RequisitionDate,
                RequestedByName = r.RequestedBy?.FirstName + " " + r.RequestedBy?.LastName,
                RequiredDate = r.RequiredDate,
                Status = r.Status,
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

            var items = await _purchaseRequisitionItemRepository.GetItemsByRequisitionIdAsync(id);

            // Group items by preferred business partner
            var supplierGroups = items
                .Where(i => i.PreferredBusinessPartnerId.HasValue)
                .GroupBy(i => i.PreferredBusinessPartnerId!.Value);

            var purchaseOrderDtos = new List<CreatePurchaseOrderDto>();

            foreach (var group in supplierGroups)
            {
                var supplierId = group.Key;
                var supplierItems = group.ToList();

                var purchaseOrderDto = new CreatePurchaseOrderDto
                {
                    SupplierId = supplierId,
                    RequiredDate = supplierItems.Min(i => i.RequiredDate ?? DateTime.UtcNow.AddDays(30)),
                    PaymentTerms = "Net 30", // Default, should be from supplier
                    Notes = $"Created from Purchase Requisition: {requisition.RequisitionNumber}",
                    ReferenceNumber = requisition.RequisitionNumber,
                    RequestedById = requisition.RequestedById,
                    Items = supplierItems.Select(item => new CreatePurchaseOrderItemDto
                    {
                        InventoryItemId = item.InventoryItemId ?? Guid.Empty,
                        ItemDescription = item.ItemDescription,
                        OrderedQuantity = item.Quantity,
                        UnitPrice = item.EstimatedUnitPrice,
                        ExpectedDeliveryDate = item.RequiredDate,
                        Notes = item.Notes
                    }).ToList()
                };

                purchaseOrderDtos.Add(purchaseOrderDto);
            }

            // For this example, return the first purchase order DTO
            // In a real implementation, you might create all the POs and return their IDs
            if (purchaseOrderDtos.Any())
            {
                // Update requisition status to indicate it's being processed
                await _purchaseRequisitionRepository.UpdateStatusAsync(id, "Converting to PO");

                return Ok(purchaseOrderDtos.First());
            }
            else
            {
                return BadRequest("No items with preferred suppliers found to create purchase orders");
            }
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
            Priority = requisition.Priority,
            Department = requisition.Department,
            CostCenter = requisition.CostCenter,
            Justification = requisition.Justification,
            Notes = requisition.Notes,
            ApprovedByName = requisition.ApprovedBy?.FirstName + " " + requisition.ApprovedBy?.LastName,
            ApprovedAt = requisition.ApprovedAt,
            RejectionReason = requisition.RejectionReason,
            TotalAmount = requisition.TotalAmount,
            ItemCount = items.Count(),
            Items = items.Select(item => new PurchaseRequisitionItemDto
            {
                Id = item.Id,
                RequisitionId = item.RequisitionId,
                InventoryItemId = item.InventoryItemId,
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
