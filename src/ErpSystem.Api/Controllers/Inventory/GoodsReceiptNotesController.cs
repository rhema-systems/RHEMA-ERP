using System.Security.Claims;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing Goods Receipt Notes (GRN)
/// </summary>
[ApiController]
[Route("api/inventory/goods-receipt-notes")]
[Authorize]
public class GoodsReceiptNotesController : ControllerBase
{
    private readonly IGoodsReceiptNoteService _grnService;
    private readonly ILogger<GoodsReceiptNotesController> _logger;

    public GoodsReceiptNotesController(
        IGoodsReceiptNoteService grnService,
        ILogger<GoodsReceiptNotesController> logger)
    {
        _grnService = grnService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Gets all goods receipt notes with optional date filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GoodsReceiptNoteDto>>> GetAll(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var grns = await _grnService.GetAllAsync(fromDate, toDate);
            return Ok(grns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goods receipt notes");
            return StatusCode(500, "An error occurred while retrieving goods receipt notes");
        }
    }

    /// <summary>
    /// Gets a goods receipt note by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<GoodsReceiptNoteDetailDto>> GetById(Guid id)
    {
        try
        {
            var grn = await _grnService.GetByIdAsync(id);
            if (grn == null)
                return NotFound($"Goods receipt note with ID {id} not found");

            return Ok(grn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goods receipt note {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the goods receipt note");
        }
    }

    /// <summary>
    /// Gets a goods receipt note by GRN number
    /// </summary>
    [HttpGet("by-number/{grnNumber}")]
    public async Task<ActionResult<GoodsReceiptNoteDetailDto>> GetByNumber(string grnNumber)
    {
        try
        {
            var grn = await _grnService.GetByGRNNumberAsync(grnNumber);
            if (grn == null)
                return NotFound($"Goods receipt note with number '{grnNumber}' not found");

            return Ok(grn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goods receipt note {GRNNumber}", grnNumber);
            return StatusCode(500, "An error occurred while retrieving the goods receipt note");
        }
    }

    /// <summary>
    /// Gets goods receipt notes by warehouse
    /// </summary>
    [HttpGet("by-warehouse/{warehouseId}")]
    public async Task<ActionResult<IEnumerable<GoodsReceiptNoteDto>>> GetByWarehouse(Guid warehouseId)
    {
        try
        {
            var grns = await _grnService.GetByWarehouseAsync(warehouseId);
            return Ok(grns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goods receipt notes for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving goods receipt notes");
        }
    }

    /// <summary>
    /// Gets goods receipt notes by supplier
    /// </summary>
    [HttpGet("by-supplier/{supplierId}")]
    public async Task<ActionResult<IEnumerable<GoodsReceiptNoteDto>>> GetBySupplier(Guid supplierId)
    {
        try
        {
            var grns = await _grnService.GetBySupplierAsync(supplierId);
            return Ok(grns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goods receipt notes for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while retrieving goods receipt notes");
        }
    }

    /// <summary>
    /// Gets goods receipt notes by purchase order
    /// </summary>
    [HttpGet("by-purchase-order/{purchaseOrderId}")]
    public async Task<ActionResult<IEnumerable<GoodsReceiptNoteDto>>> GetByPurchaseOrder(Guid purchaseOrderId)
    {
        try
        {
            var grns = await _grnService.GetByPurchaseOrderAsync(purchaseOrderId);
            return Ok(grns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goods receipt notes for PO {PurchaseOrderId}", purchaseOrderId);
            return StatusCode(500, "An error occurred while retrieving goods receipt notes");
        }
    }

    /// <summary>
    /// Gets goods receipt notes pending inspection
    /// </summary>
    [HttpGet("pending-inspection")]
    public async Task<ActionResult<IEnumerable<GoodsReceiptNoteDto>>> GetPendingInspection()
    {
        try
        {
            var grns = await _grnService.GetPendingInspectionAsync();
            return Ok(grns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending inspection GRNs");
            return StatusCode(500, "An error occurred while retrieving pending inspection GRNs");
        }
    }

    /// <summary>
    /// Creates a new goods receipt note
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GoodsReceiptNoteDto>> Create([FromBody] CreateGoodsReceiptNoteDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var grn = await _grnService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = grn.Id }, grn);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating goods receipt note");
            return StatusCode(500, "An error occurred while creating the goods receipt note");
        }
    }

    /// <summary>
    /// Submits a goods receipt note for inspection
    /// </summary>
    [HttpPost("{id}/submit-for-inspection")]
    public async Task<ActionResult> SubmitForInspection(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _grnService.SubmitForInspectionAsync(id, userId);
            if (!result)
                return BadRequest("Failed to submit GRN for inspection");

            return Ok(new { message = "GRN submitted for inspection successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting GRN {Id} for inspection", id);
            return StatusCode(500, "An error occurred while submitting the GRN for inspection");
        }
    }

    /// <summary>
    /// Updates inspection result for a GRN item
    /// </summary>
    [HttpPost("{id}/update-inspection")]
    public async Task<ActionResult> UpdateInspection(Guid id, [FromBody] UpdateGRNInspectionDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _grnService.UpdateInspectionResultAsync(dto, userId);
            if (!result)
                return BadRequest("Failed to update inspection result");

            return Ok(new { message = "Inspection result updated successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection for GRN {Id}", id);
            return StatusCode(500, "An error occurred while updating the inspection result");
        }
    }

    /// <summary>
    /// Completes inspection for a goods receipt note
    /// </summary>
    [HttpPost("{id}/complete-inspection")]
    public async Task<ActionResult> CompleteInspection(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _grnService.CompleteInspectionAsync(id, userId);
            if (!result)
                return BadRequest("Failed to complete inspection");

            return Ok(new { message = "Inspection completed successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing inspection for GRN {Id}", id);
            return StatusCode(500, "An error occurred while completing the inspection");
        }
    }

    /// <summary>
    /// Posts a goods receipt note to inventory
    /// </summary>
    [HttpPost("{id}/post-to-inventory")]
    public async Task<ActionResult> PostToInventory(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _grnService.PostToInventoryAsync(id, userId);
            if (!result)
                return BadRequest("Failed to post GRN to inventory");

            return Ok(new { message = "GRN posted to inventory successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting GRN {Id} to inventory", id);
            return StatusCode(500, "An error occurred while posting the GRN to inventory");
        }
    }

    /// <summary>
    /// Cancels a goods receipt note
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromBody] CancelGRNDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _grnService.CancelAsync(id, dto.Reason, userId);
            if (!result)
                return BadRequest("Failed to cancel GRN");

            return Ok(new { message = "GRN cancelled successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling GRN {Id}", id);
            return StatusCode(500, "An error occurred while cancelling the GRN");
        }
    }
}

/// <summary>
/// DTO for cancelling a GRN
/// </summary>
public class CancelGRNDto
{
    public string Reason { get; set; } = string.Empty;
}

