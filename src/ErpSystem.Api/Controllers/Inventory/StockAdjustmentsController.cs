using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing stock adjustments
/// Handles positive and negative inventory adjustments outside of normal purchasing/requisition flows
/// </summary>
[ApiController]
[Route("api/inventory/adjustments")]
[Authorize(Policy = "InternalOnly")]
public class StockAdjustmentsController : ControllerBase
{
    private readonly IStockAdjustmentService _adjustmentService;
    private readonly ILogger<StockAdjustmentsController> _logger;

    public StockAdjustmentsController(
        IStockAdjustmentService adjustmentService,
        ILogger<StockAdjustmentsController> logger)
    {
        _adjustmentService = adjustmentService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all stock adjustments with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StockAdjustmentDto>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? reasonCode = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var adjustments = await _adjustmentService.GetAllAsync(status, reasonCode, startDate, endDate);
            return Ok(adjustments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock adjustments");
            return StatusCode(500, "An error occurred while retrieving stock adjustments");
        }
    }

    /// <summary>
    /// Gets a stock adjustment by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> GetById(Guid id)
    {
        try
        {
            var adjustment = await _adjustmentService.GetByIdAsync(id);
            if (adjustment == null)
                return NotFound($"Stock adjustment with ID {id} not found");

            return Ok(adjustment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock adjustment {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the stock adjustment");
        }
    }

    /// <summary>
    /// Gets a stock adjustment by adjustment number
    /// </summary>
    [HttpGet("by-number/{adjustmentNumber}")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> GetByNumber(string adjustmentNumber)
    {
        try
        {
            var adjustment = await _adjustmentService.GetByAdjustmentNumberAsync(adjustmentNumber);
            if (adjustment == null)
                return NotFound($"Stock adjustment with number '{adjustmentNumber}' not found");

            return Ok(adjustment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock adjustment {AdjustmentNumber}", adjustmentNumber);
            return StatusCode(500, "An error occurred while retrieving the stock adjustment");
        }
    }

    /// <summary>
    /// Gets pending (draft) adjustments
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<StockAdjustmentDto>>> GetPending()
    {
        try
        {
            var adjustments = await _adjustmentService.GetPendingAsync();
            return Ok(adjustments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending stock adjustments");
            return StatusCode(500, "An error occurred while retrieving pending adjustments");
        }
    }

    /// <summary>
    /// Gets available reason codes for stock adjustments
    /// </summary>
    [HttpGet("reason-codes")]
    public ActionResult<Dictionary<string, string>> GetReasonCodes()
    {
        return Ok(StockAdjustmentReasonCodes.ReasonCodeDescriptions);
    }

    /// <summary>
    /// Creates a new stock adjustment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Create([FromBody] CreateStockAdjustmentDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var adjustment = await _adjustmentService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = adjustment.Id }, adjustment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock adjustment");
            return StatusCode(500, "An error occurred while creating the stock adjustment");
        }
    }

    /// <summary>
    /// Updates an existing stock adjustment (only if in Draft status)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Update(Guid id, [FromBody] UpdateStockAdjustmentDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var adjustment = await _adjustmentService.UpdateAsync(id, dto, userId);
            return Ok(adjustment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock adjustment {Id}", id);
            return StatusCode(500, "An error occurred while updating the stock adjustment");
        }
    }

    /// <summary>
    /// Deletes a stock adjustment (only if in Draft status)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _adjustmentService.DeleteAsync(id, userId);
            return Ok(new { message = "Stock adjustment deleted successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting stock adjustment {Id}", id);
            return StatusCode(500, "An error occurred while deleting the stock adjustment");
        }
    }

    /// <summary>
    /// Submits a draft stock adjustment to the shared approval workflow.
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Submit(Guid id, [FromBody] StockAdjustmentActionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var adjustment = await _adjustmentService.SubmitAsync(id, userId, request);
            return Ok(adjustment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting stock adjustment {Id}", id);
            return StatusCode(500, "An error occurred while submitting the stock adjustment");
        }
    }

    [HttpPost("{id}/decision")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Decide(Guid id, [FromBody] DecideStockAdjustmentRequest request)
    {
        try { return Ok(await _adjustmentService.DecideAsync(id, GetCurrentUserId(), request)); }
        catch (ArgumentException ex) { return NotFound(new { code = "INV_ADJUSTMENT_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InvalidOperationException ex) { return Conflict(new { code = "INV_ADJUSTMENT_DECISION_REJECTED", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }

    /// <summary>
    /// Posts a stock adjustment (applies the adjustment to inventory)
    /// </summary>
    [HttpPost("{id}/post")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Post(Guid id, [FromBody] StockAdjustmentActionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var adjustment = await _adjustmentService.PostAsync(id, userId, request);
            return Ok(adjustment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting stock adjustment {Id}", id);
            return StatusCode(500, "An error occurred while posting the stock adjustment");
        }
    }

    [HttpPost("{id}/reverse")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Reverse(Guid id, [FromBody] ReverseStockAdjustmentRequest request)
    {
        try { return Ok(await _adjustmentService.ReverseAsync(id, GetCurrentUserId(), request)); }
        catch (ArgumentException ex) { return NotFound(new { code = "INV_ADJUSTMENT_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InvalidOperationException ex) { return Conflict(new { code = "INV_ADJUSTMENT_REVERSAL_REJECTED", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }

    /// <summary>
    /// Deletes an item from a stock adjustment (only if in Draft status)
    /// </summary>
    [HttpDelete("{id}/items/{itemId}")]
    public async Task<ActionResult> DeleteItem(Guid id, Guid itemId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _adjustmentService.DeleteItemAsync(id, itemId, userId);
            return Ok(new { message = "Item deleted successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item {ItemId} from stock adjustment {Id}", itemId, id);
            return StatusCode(500, "An error occurred while deleting the item");
        }
    }

    /// <summary>
    /// Cancels a stock adjustment (only if not yet posted)
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<StockAdjustmentDetailDto>> Cancel(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var adjustment = await _adjustmentService.CancelAsync(id, userId);
            return Ok(adjustment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling stock adjustment {Id}", id);
            return StatusCode(500, "An error occurred while cancelling the stock adjustment");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("userId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }
}
