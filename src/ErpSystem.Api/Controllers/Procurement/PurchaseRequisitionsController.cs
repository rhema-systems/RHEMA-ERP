using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Common;

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
    private readonly ILogger<PurchaseRequisitionsController> _logger;

    public PurchaseRequisitionsController(
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IPurchaseRequisitionItemRepository purchaseRequisitionItemRepository,
        ILogger<PurchaseRequisitionsController> logger)
    {
        _purchaseRequisitionRepository = purchaseRequisitionRepository;
        _purchaseRequisitionItemRepository = purchaseRequisitionItemRepository;
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
                    PreferredSupplierId = item.PreferredSupplierId,
                    PreferredSupplierName = item.PreferredSupplier?.Name,
                    Notes = item.Notes,
                    Specifications = item.Specifications,
                    Status = item.Status,
                    PurchaseOrderId = item.PurchaseOrderId,
                    PurchaseOrderNumber = item.PurchaseOrder?.OrderNumber,
                    ItemCode = item.InventoryItem?.ItemCode,
                    ItemName = item.InventoryItem?.Name
                }).ToList()
            };

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

            // Generate requisition number
            var requisitionNumber = await _purchaseRequisitionRepository.GenerateRequisitionNumberAsync();

            // Calculate total amount
            var totalAmount = createDto.Items.Sum(item => item.Quantity * item.EstimatedUnitPrice);

            var requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(),
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
                    RequisitionId = requisition.Id,
                    InventoryItemId = itemDto.InventoryItemId,
                    ItemDescription = itemDto.ItemDescription,
                    Quantity = itemDto.Quantity,
                    UnitOfMeasure = itemDto.UnitOfMeasure,
                    EstimatedUnitPrice = itemDto.EstimatedUnitPrice,
                    LineTotal = itemDto.Quantity * itemDto.EstimatedUnitPrice,
                    RequiredDate = itemDto.RequiredDate,
                    PreferredSupplierId = itemDto.PreferredSupplierId,
                    Notes = itemDto.Notes,
                    Specifications = itemDto.Specifications,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _purchaseRequisitionItemRepository.CreateItemAsync(item);
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

            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            if (requisition.Status != "Pending Approval" && requisition.Status != "Draft")
            {
                return BadRequest($"Purchase requisition cannot be approved in current status: {requisition.Status}");
            }

            if (approvalDto.Approved)
            {
                await _purchaseRequisitionRepository.UpdateStatusAsync(id, "Approved");
            }
            else
            {
                await _purchaseRequisitionRepository.UpdateStatusAsync(id, "Rejected");
                if (!string.IsNullOrEmpty(approvalDto.RejectionReason))
                {
                    requisition.RejectionReason = approvalDto.RejectionReason;
                    await _purchaseRequisitionRepository.UpdateRequisitionAsync(requisition);
                }
            }

            return NoContent();
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
            var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(id);
            if (requisition == null)
            {
                return NotFound($"Purchase requisition with ID {id} not found");
            }

            if (requisition.Status != "Draft")
            {
                return BadRequest($"Purchase requisition cannot be submitted in current status: {requisition.Status}");
            }

            await _purchaseRequisitionRepository.UpdateStatusAsync(id, "Pending Approval");

            return NoContent();
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

            // Group items by preferred supplier
            var supplierGroups = items
                .Where(i => i.PreferredSupplierId.HasValue)
                .GroupBy(i => i.PreferredSupplierId.Value);

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

    #region Private Helper Methods

    private async Task<PurchaseRequisitionDetailDto> GetPurchaseRequisitionDetailDto(Guid requisitionId)
    {
        var requisition = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(requisitionId);
        if (requisition == null) return null!;

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
                PreferredSupplierId = item.PreferredSupplierId,
                PreferredSupplierName = item.PreferredSupplier?.Name,
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

    #endregion
}