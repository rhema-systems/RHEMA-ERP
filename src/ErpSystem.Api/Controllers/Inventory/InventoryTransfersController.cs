using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing inventory transfers between warehouses
/// </summary>
[ApiController]
[Route("api/inventory/transfers")]
[Authorize(Policy = "InternalOnly")]
public class InventoryTransfersController : ControllerBase
{
    private readonly IInventoryTransferService _transferService;
    private readonly TransferDocumentService _documentService;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<InventoryTransfersController> _logger;

    public InventoryTransfersController(
        IInventoryTransferService transferService,
        TransferDocumentService documentService,
        IWorkflowService workflowService,
        ILogger<InventoryTransfersController> logger)
    {
        _transferService = transferService;
        _documentService = documentService;
        _workflowService = workflowService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Gets all inventory transfers with optional date filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryTransferDto>>> GetAll(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var transfers = (await _transferService.GetAllAsync(fromDate, toDate)).ToList();

            // Provide more accurate UX for pending approvals: show the actual current workflow step name.
            var pending = transfers.Where(t => t.Status == TransferStatus.Submitted).ToList();
            if (pending.Count > 0)
            {
                await Task.WhenAll(pending.Select(async t =>
                {
                    try
                    {
                        var step = await _workflowService.GetCurrentWorkflowStepAsync("InventoryTransfer", t.Id);
                        t.CurrentWorkflowStepName = step?.StepName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to resolve current workflow step for InventoryTransfer {TransferId}", t.Id);
                    }
                }));
            }

            return Ok(transfers);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving inventory transfers");
            return StatusCode(500, "An error occurred while retrieving inventory transfers");
        }
    }

    /// <summary>
    /// Gets an inventory transfer by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryTransferDetailDto>> GetById(Guid id)
    {
        try
        {
            var transfer = await _transferService.GetByIdAsync(id);
            if (transfer == null)
                return NotFound($"Inventory transfer with ID {id} not found");

            if (transfer.Status == TransferStatus.Submitted)
            {
                try
                {
                    var step = await _workflowService.GetCurrentWorkflowStepAsync("InventoryTransfer", id);
                    transfer.CurrentWorkflowStepName = step?.StepName;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve current workflow step for InventoryTransfer {TransferId}", id);
                }
            }

            return Ok(transfer);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving inventory transfer {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the inventory transfer");
        }
    }

    /// <summary>
    /// Gets an inventory transfer by transfer number
    /// </summary>
    [HttpGet("by-number/{transferNumber}")]
    public async Task<ActionResult<InventoryTransferDetailDto>> GetByNumber(string transferNumber)
    {
        try
        {
            var transfer = await _transferService.GetByTransferNumberAsync(transferNumber);
            if (transfer == null)
                return NotFound($"Inventory transfer with number '{transferNumber}' not found");

            return Ok(transfer);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving inventory transfer {TransferNumber}", transferNumber);
            return StatusCode(500, "An error occurred while retrieving the inventory transfer");
        }
    }

    /// <summary>
    /// Gets inventory transfers by warehouse
    /// </summary>
    [HttpGet("by-warehouse/{warehouseId}")]
    public async Task<ActionResult<IEnumerable<InventoryTransferDto>>> GetByWarehouse(
        Guid warehouseId,
        [FromQuery] bool isSource = true)
    {
        try
        {
            var transfers = await _transferService.GetByWarehouseAsync(warehouseId, isSource);
            return Ok(transfers);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving transfers for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving inventory transfers");
        }
    }

    /// <summary>
    /// Gets inventory transfers in transit
    /// </summary>
    [HttpGet("in-transit")]
    public async Task<ActionResult<IEnumerable<InventoryTransferDto>>> GetInTransit()
    {
        try
        {
            var transfers = await _transferService.GetInTransitAsync();
            return Ok(transfers);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving in-transit transfers");
            return StatusCode(500, "An error occurred while retrieving in-transit transfers");
        }
    }

    /// <summary>
    /// Gets inventory transfers pending approval
    /// </summary>
    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<InventoryTransferDto>>> GetPendingApproval()
    {
        try
        {
            var transfers = await _transferService.GetPendingApprovalAsync();
            return Ok(transfers);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving pending approval transfers");
            return StatusCode(500, "An error occurred while retrieving pending approval transfers");
        }
    }

    /// <summary>
    /// Creates a new inventory transfer
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryTransferDto>> Create([FromBody] CreateInventoryTransferDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var transfer = await _transferService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = transfer.Id }, transfer);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating inventory transfer");
            return StatusCode(500, "An error occurred while creating the inventory transfer");
        }
    }

    /// <summary>
    /// Updates an inventory transfer (only in Draft status)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<InventoryTransferDto>> Update(Guid id, [FromBody] UpdateInventoryTransferDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var transfer = await _transferService.UpdateAsync(id, dto, userId);
            return Ok(transfer);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error updating inventory transfer {Id}", id);
            return StatusCode(500, "An error occurred while updating the inventory transfer");
        }
    }

    /// <summary>
    /// Submits an inventory transfer for approval
    /// </summary>
    [HttpPost("{id}/submit-for-approval")]
    public async Task<ActionResult> SubmitForApproval(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.SubmitForApprovalAsync(id, userId);
            if (!result)
                return BadRequest("Failed to submit transfer for approval");

            return Ok(new { message = "Transfer submitted for approval successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error submitting transfer {Id} for approval", id);
            return StatusCode(500, "An error occurred while submitting the transfer for approval");
        }
    }

    /// <summary>
    /// Approves an inventory transfer
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(Guid id, [FromBody] ApproveTransferDto? dto = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.ApproveAsync(id, userId, dto?.Comments);
            if (!result)
                return BadRequest("Failed to approve transfer");

            return Ok(new { message = "Transfer approved successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error approving transfer {Id}", id);
            return StatusCode(500, "An error occurred while approving the transfer");
        }
    }

    /// <summary>
    /// Rejects an inventory transfer
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] RejectTransferDto dto)
    {
        try
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
            {
                return BadRequest("Rejection comment is required.");
            }

            var userId = GetCurrentUserId();
            var result = await _transferService.RejectAsync(id, dto.Reason, userId, dto.Reason);
            if (!result)
                return BadRequest("Failed to reject transfer");

            return Ok(new { message = "Transfer rejected successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error rejecting transfer {Id}", id);
            return StatusCode(500, "An error occurred while rejecting the transfer");
        }
    }

    /// <summary>
    /// Ships an inventory transfer
    /// </summary>
    [HttpPost("{id}/ship")]
    public async Task<ActionResult> Ship(Guid id, [FromBody] ShipTransferDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var shippedItems = dto.Items?.ToDictionary(i => i.ItemId, i => i.ShippedQuantity);
            var control = dto.ToControl();
            control.NegativeStockOverrideIds = dto.Items?
                .Where(item => item.NegativeStockOverrideId.HasValue)
                .ToDictionary(item => item.ItemId, item => item.NegativeStockOverrideId!.Value) ?? new();
            var result = await _transferService.ShipAsync(id, userId, dto.TrackingNumber, shippedItems, control);
            if (!result)
                return BadRequest("Failed to ship transfer");

            return Ok(new { message = "Transfer shipped successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InventoryTrackingAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRACKING_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryTrackingControlException ex)
        {
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error shipping transfer {Id}", id);
            return StatusCode(500, "An error occurred while shipping the transfer");
        }
    }

    /// <summary>
    /// Ships an inventory transfer with shipping costs and cost allocation
    /// </summary>
    [HttpPost("{id}/ship-with-costs")]
    public async Task<ActionResult> ShipWithCosts(Guid id, [FromBody] ShipTransferWithCostsDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.ShipWithCostsAsync(id, userId, dto);
            if (!result)
                return BadRequest("Failed to ship transfer with costs");

            return Ok(new { message = "Transfer shipped with costs successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InventoryTrackingAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRACKING_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryTrackingControlException ex)
        {
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error shipping transfer {Id} with costs", id);
            return StatusCode(500, "An error occurred while shipping the transfer with costs");
        }
    }

    /// <summary>
    /// Saves shipping costs and details without shipping the transfer.
    /// Allows preparing shipment information before actually shipping.
    /// </summary>
    [HttpPost("{id}/save-shipping-costs")]
    public async Task<ActionResult> SaveShippingCosts(Guid id, [FromBody] ShipTransferWithCostsDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.SaveShippingCostsAsync(id, userId, dto);
            if (!result)
                return BadRequest("Failed to save shipping costs");

            return Ok(new { message = "Shipping costs saved successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error saving shipping costs for transfer {Id}", id);
            return StatusCode(500, "An error occurred while saving shipping costs");
        }
    }

    /// <summary>
    /// Receives an inventory transfer
    /// </summary>
    [HttpPost("{id}/receive")]
    public async Task<ActionResult> Receive(Guid id, [FromBody] ReceiveTransferDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.ReceiveAsync(id, userId, dto.ReceivedItems, dto.ToControl());
            if (!result)
                return BadRequest("Failed to receive transfer");

            return Ok(new { message = "Transfer received successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InventoryTrackingAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRACKING_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryTrackingControlException ex)
        {
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error receiving transfer {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Cancels an inventory transfer
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromBody] CancelTransferDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.CancelAsync(id, dto.Reason, userId, dto.ToControl());
            if (!result)
                return BadRequest("Failed to cancel transfer");

            return Ok(new { message = "Transfer cancelled successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error cancelling transfer {Id}", id);
            return StatusCode(500, "An error occurred while cancelling the transfer");
        }
    }

    /// <summary>
    /// Reverses a shipment that is in transit (not yet received).
    /// Reinstates quantities to the source warehouse and allows creating a new shipment.
    /// </summary>
    [HttpPost("{id}/reverse-shipment")]
    public async Task<ActionResult> ReverseShipment(Guid id, [FromBody] ReverseShipmentDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.ReverseShipmentAsync(id, dto.Reason, userId, dto.ToControl());
            if (!result)
                return BadRequest("Failed to reverse shipment");

            return Ok(new { message = "Shipment reversed and transfer cancelled successfully." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error reversing shipment for transfer {Id}", id);
            return StatusCode(500, "An error occurred while reversing the shipment");
        }
    }

    [HttpGet("discrepancy-reasons")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetDiscrepancyReasons() =>
        Ok(InventoryTransferDiscrepancyReasonCodes.All);

    [HttpGet("discrepancy-resolutions")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetDiscrepancyResolutions() =>
        Ok(InventoryTransferDiscrepancyResolutionCodes.All);

    [HttpPost("{id}/resolve-discrepancies")]
    public async Task<ActionResult> ResolveDiscrepancies(Guid id, [FromBody] ResolveInventoryTransferDiscrepancyRequest request)
    {
        try
        {
            await _transferService.ResolveDiscrepanciesAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = "Transfer discrepancies resolved successfully" });
        }
        catch (ArgumentException ex) { return NotFound(new { code = "INV_TRANSFER_DISCREPANCY_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InvalidOperationException ex) { return Conflict(new { code = "INV_TRANSFER_DISCREPANCY_CONFLICT", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }

    [HttpPost("{id}/close")]
    public async Task<ActionResult> Close(Guid id, [FromBody] CloseInventoryTransferRequest request)
    {
        try
        {
            await _transferService.CloseAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = "Transfer closed successfully" });
        }
        catch (ArgumentException ex) { return NotFound(new { code = "INV_TRANSFER_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRANSFER_SOD", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InvalidOperationException ex) { return Conflict(new { code = "INV_TRANSFER_CLOSE_CONFLICT", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }

    /// <summary>
    /// Generates a Shipment Note PDF for the transfer
    /// </summary>
    [HttpGet("{id}/shipment-note")]
    public async Task<ActionResult> GetShipmentNote(Guid id)
    {
        try
        {
            var pdfBytes = await _documentService.GenerateShipmentNoteAsync(id);
            return File(pdfBytes, "application/pdf", $"ShipmentNote-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error generating shipment note for transfer {Id}", id);
            return StatusCode(500, "An error occurred while generating the shipment note");
        }
    }

    /// <summary>
    /// Generates a Goods Received Note (GRN) PDF for the transfer
    /// </summary>
    [HttpGet("{id}/grn")]
    public async Task<ActionResult> GetGoodsReceivedNote(Guid id)
    {
        try
        {
            var pdfBytes = await _documentService.GenerateGoodsReceivedNoteAsync(id);
            return File(pdfBytes, "application/pdf", $"GRN-{id}.pdf");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error generating GRN for transfer {Id}", id);
            return StatusCode(500, "An error occurred while generating the GRN");
        }
    }

    /// <summary>
    /// Adds an item to an inventory transfer (only allowed in Draft status)
    /// </summary>
    [HttpPost("{id}/items")]
    public async Task<ActionResult<InventoryTransferItemDto>> AddItem(Guid id, [FromBody] AddTransferItemDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.AddItemAsync(id, dto, userId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error adding item to transfer {Id}", id);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    /// <summary>
    /// Updates an item on an inventory transfer (only allowed in Draft status)
    /// </summary>
    [HttpPut("{id}/items/{itemId}")]
    public async Task<ActionResult<InventoryTransferItemDto>> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateTransferItemDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.UpdateItemAsync(id, itemId, dto, userId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error updating item on transfer {Id}", id);
            return StatusCode(500, "An error occurred while updating the item");
        }
    }

    /// <summary>
    /// Removes an item from an inventory transfer (only allowed in Draft status)
    /// </summary>
    [HttpDelete("{id}/items/{itemId}")]
    public async Task<ActionResult> RemoveItem(Guid id, Guid itemId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _transferService.RemoveItemAsync(id, itemId, userId);
            if (!result)
                return BadRequest("Failed to remove item from transfer");

            return Ok(new { message = "Item removed successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error removing item from transfer {Id}", id);
            return StatusCode(500, "An error occurred while removing the item");
        }
    }
}

/// <summary>
/// DTO for rejecting a transfer
/// </summary>
public class RejectTransferDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for approving a transfer (optional comment)
/// </summary>
public class ApproveTransferDto
{
    public string? Comments { get; set; }
}

/// <summary>
/// DTO for shipping a transfer
/// </summary>
public abstract class TransferMutationDto
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }

    public InventoryTransferMutationContext ToControl() => new()
    {
        RowVersion = RowVersion,
        IdempotencyKey = IdempotencyKey,
        CorrelationId = CorrelationId,
        Comment = Comment
    };
}

public class ShipTransferDto : TransferMutationDto
{
    public string? TrackingNumber { get; set; }
    [Required, MinLength(1)]
    public List<ShipTransferItemDto>? Items { get; set; }
}

/// <summary>
/// DTO for shipped item quantities
/// </summary>
public class ShipTransferItemDto
{
    [Required]
    public Guid ItemId { get; set; }
    [Range(0.0001, double.MaxValue)]
    public decimal ShippedQuantity { get; set; }
    public Guid? NegativeStockOverrideId { get; set; }
}

/// <summary>
/// DTO for receiving a transfer
/// </summary>
public class ReceiveTransferDto : TransferMutationDto
{
    [Required, MinLength(1)]
    public List<InventoryTransferItemDto>? ReceivedItems { get; set; }
}

/// <summary>
/// DTO for cancelling a transfer
/// </summary>
public class CancelTransferDto : TransferMutationDto
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for reversing a shipment
/// </summary>
public class ReverseShipmentDto : TransferMutationDto
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
