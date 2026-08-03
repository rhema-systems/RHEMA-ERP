using System.Security.Claims;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing physical inventory counts
/// </summary>
[ApiController]
[Route("api/inventory/physical-counts")]
[Authorize(Policy = "InternalOnly")]
public class PhysicalCountsController : ControllerBase
{
    private readonly IPhysicalCountService _countService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PhysicalCountsController> _logger;

    public PhysicalCountsController(
        IPhysicalCountService countService,
        ICurrentUserService currentUser,
        ILogger<PhysicalCountsController> logger)
    {
        _countService = countService;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Gets all physical counts with optional date filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PhysicalCountDto>>> GetAll(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var counts = await _countService.GetAllAsync(fromDate, toDate);
            return Ok(counts);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving physical counts");
            return StatusCode(500, "An error occurred while retrieving physical counts");
        }
    }

    /// <summary>
    /// Gets physical counts with advanced filtering
    /// </summary>
    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<PhysicalCountDto>>> GetFiltered([FromQuery] PhysicalCountFilterDto filter)
    {
        try
        {
            var counts = await _countService.GetFilteredAsync(filter);
            return Ok(counts);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving filtered physical counts");
            return StatusCode(500, "An error occurred while retrieving physical counts");
        }
    }

    /// <summary>
    /// Gets a physical count by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PhysicalCountDetailDto>> GetById(Guid id)
    {
        try
        {
            var count = await _countService.GetByIdAsync(id);
            if (count == null)
                return NotFound($"Physical count with ID {id} not found");

            return Ok(count);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving physical count {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the physical count");
        }
    }

    /// <summary>
    /// Gets a physical count by count number
    /// </summary>
    [HttpGet("by-number/{countNumber}")]
    public async Task<ActionResult<PhysicalCountDetailDto>> GetByNumber(string countNumber)
    {
        try
        {
            var count = await _countService.GetByCountNumberAsync(countNumber);
            if (count == null)
                return NotFound($"Physical count with number '{countNumber}' not found");

            return Ok(count);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving physical count {CountNumber}", countNumber);
            return StatusCode(500, "An error occurred while retrieving the physical count");
        }
    }

    /// <summary>
    /// Gets physical counts by warehouse
    /// </summary>
    [HttpGet("by-warehouse/{warehouseId}")]
    public async Task<ActionResult<IEnumerable<PhysicalCountDto>>> GetByWarehouse(Guid warehouseId)
    {
        try
        {
            var counts = await _countService.GetByWarehouseAsync(warehouseId);
            return Ok(counts);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving physical counts for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving physical counts");
        }
    }

    /// <summary>
    /// Gets physical counts in progress
    /// </summary>
    [HttpGet("in-progress")]
    public async Task<ActionResult<IEnumerable<PhysicalCountDto>>> GetInProgress()
    {
        try
        {
            var counts = await _countService.GetInProgressAsync();
            return Ok(counts);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving in-progress physical counts");
            return StatusCode(500, "An error occurred while retrieving in-progress physical counts");
        }
    }

    /// <summary>
    /// Gets items with variance for a physical count
    /// </summary>
    [HttpGet("{id}/items-with-variance")]
    public async Task<ActionResult<IEnumerable<PhysicalCountItemDto>>> GetItemsWithVariance(Guid id)
    {
        try
        {
            var items = await _countService.GetItemsWithVarianceAsync(id);
            return Ok(items);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving items with variance for count {Id}", id);
            return StatusCode(500, "An error occurred while retrieving items with variance");
        }
    }

    /// <summary>
    /// Creates a new physical count
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PhysicalCountDto>> Create([FromBody] CreatePhysicalCountDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _countService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = count.Id }, count);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating physical count");
            return StatusCode(500, "An error occurred while creating the physical count");
        }
    }

    /// <summary>
    /// Starts a physical count
    /// </summary>
    [HttpPost("{id}/start")]
    public async Task<ActionResult> Start(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.StartCountAsync(id, userId);
            if (!result)
                return BadRequest("Failed to start physical count");

            return Ok(new { message = "Physical count started successfully" });
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
            _logger.LogError(ex, "Error starting physical count {Id}", id);
            return StatusCode(500, "An error occurred while starting the physical count");
        }
    }

    /// <summary>
    /// Records a count for a single item
    /// </summary>
    [HttpPost("{id}/record-item")]
    public async Task<ActionResult> RecordItem(Guid id, [FromBody] RecordCountItemDto dto)
    {
        try
        {
            var count = await _countService.GetByIdAsync(id);
            if (count is null || count.Items.All(item => item.Id != dto.PhysicalCountItemId))
                return NotFound("The count line does not belong to the requested tenant-safe physical count.");
            var userId = GetCurrentUserId();
            var result = await _countService.RecordCountItemAsync(dto, userId);
            if (!result)
                return BadRequest("Failed to record count item");

            return Ok(new { message = "Count item recorded successfully" });
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
            _logger.LogError(ex, "Error recording count item for count {Id}", id);
            return StatusCode(500, "An error occurred while recording the count item");
        }
    }

    /// <summary>
    /// Records counts for multiple items
    /// </summary>
    [HttpPost("{id}/record-items")]
    public async Task<ActionResult> RecordItems(Guid id, [FromBody] List<RecordCountItemDto> items)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.RecordCountItemsAsync(items, userId);
            if (!result)
                return BadRequest("Failed to record count items");

            return Ok(new { message = "Count items recorded successfully" });
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
            _logger.LogError(ex, "Error recording count items for count {Id}", id);
            return StatusCode(500, "An error occurred while recording the count items");
        }
    }

    [HttpPost("{id}/recount")]
    public async Task<ActionResult> RecordRecount(Guid id, [FromBody] RecordPhysicalCountRecountRequest request)
    {
        try
        {
            await _countService.RecordRecountAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = "Independent recount and investigation recorded." });
        }
        catch (Exception ex) { return ControlledError(ex, "record recount", id); }
    }

    [HttpPost("{id}/stores-decision")]
    public async Task<ActionResult> DecideStores(Guid id, [FromBody] PhysicalCountDecisionRequest request)
    {
        try
        {
            await _countService.ApproveStoresAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = request.Approved ? "Stores approval recorded." : "Count returned for recount." });
        }
        catch (Exception ex) { return ControlledError(ex, "record Stores decision", id); }
    }

    [HttpPost("{id}/finance-decision")]
    public async Task<ActionResult> DecideFinance(Guid id, [FromBody] PhysicalCountDecisionRequest request)
    {
        try
        {
            await _countService.ApproveFinanceAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = request.Approved ? "Finance approval recorded." : "Count returned for recount." });
        }
        catch (Exception ex) { return ControlledError(ex, "record Finance decision", id); }
    }

    [HttpPost("{id}/audit-attestation")]
    public async Task<ActionResult> AttestAudit(Guid id, [FromBody] PhysicalCountDecisionRequest request)
    {
        try
        {
            await _countService.AttestAuditAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = request.Approved ? "Internal Audit attestation recorded." : "Audit exception returned for recount." });
        }
        catch (Exception ex) { return ControlledError(ex, "record Internal Audit attestation", id); }
    }

    [HttpPost("{id}/controlled-post")]
    public async Task<ActionResult> ControlledPost(Guid id, [FromBody] PhysicalCountMutationRequest request)
    {
        try
        {
            await _countService.PostControlledAdjustmentsAsync(id, GetCurrentUserId(), request);
            return Ok(new { message = "Count posted through the authoritative stock-adjustment and Finance owners." });
        }
        catch (Exception ex) { return ControlledError(ex, "post controlled variance", id); }
    }

    [HttpGet("cycle-schedules")]
    public async Task<ActionResult<IReadOnlyList<InventoryCycleCountScheduleDto>>> GetCycleSchedules()
    {
        try { return Ok(await _countService.GetCycleCountSchedulesAsync()); }
        catch (Exception ex) { return ControlledError(ex, "read cycle-count schedules", Guid.Empty); }
    }

    [HttpPost("cycle-schedules")]
    public async Task<ActionResult<InventoryCycleCountScheduleDto>> CreateCycleSchedule(
        [FromBody] SaveInventoryCycleCountScheduleRequest request)
    {
        try
        {
            var saved = await _countService.SaveCycleCountScheduleAsync(null, request, GetCurrentUserId());
            return CreatedAtAction(nameof(GetCycleSchedules), new { }, saved);
        }
        catch (Exception ex) { return ControlledError(ex, "create cycle-count schedule", Guid.Empty); }
    }

    [HttpPut("cycle-schedules/{scheduleId:guid}")]
    public async Task<ActionResult<InventoryCycleCountScheduleDto>> UpdateCycleSchedule(
        Guid scheduleId,
        [FromBody] SaveInventoryCycleCountScheduleRequest request)
    {
        try { return Ok(await _countService.SaveCycleCountScheduleAsync(scheduleId, request, GetCurrentUserId())); }
        catch (Exception ex) { return ControlledError(ex, "update cycle-count schedule", scheduleId); }
    }

    [HttpPost("cycle-schedules/generate")]
    public async Task<ActionResult<CycleCountGenerationResultDto>> GenerateCycleCounts()
    {
        try
        {
            if (_currentUser.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Forbid();
            return Ok(await _countService.GenerateDueCycleCountsAsync(tenantId, DateTime.UtcNow, GetCurrentUserId()));
        }
        catch (Exception ex) { return ControlledError(ex, "generate due cycle counts", Guid.Empty); }
    }

    /// <summary>
    /// Completes a physical count
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<ActionResult> Complete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.CompleteCountAsync(id, userId);
            if (!result)
                return BadRequest("Failed to complete physical count");

            return Ok(new { message = "Physical count completed successfully" });
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
            _logger.LogError(ex, "Error completing physical count {Id}", id);
            return StatusCode(500, "An error occurred while completing the physical count");
        }
    }

    /// <summary>
    /// Approves variances for a physical count
    /// </summary>
    [HttpPost("{id}/approve-variances")]
    public async Task<ActionResult> ApproveVariances(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.ApproveVariancesAsync(id, userId);
            if (!result)
                return BadRequest("Failed to approve variances");

            return Ok(new { message = "Variances approved successfully" });
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
            _logger.LogError(ex, "Error approving variances for count {Id}", id);
            return StatusCode(500, "An error occurred while approving variances");
        }
    }

    /// <summary>
    /// Posts adjustments for a physical count
    /// </summary>
    [HttpPost("{id}/post-adjustments")]
    public async Task<ActionResult> PostAdjustments(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.PostAdjustmentsAsync(id, userId);
            if (!result)
                return BadRequest("Failed to post adjustments");

            return Ok(new { message = "Adjustments posted successfully" });
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
            _logger.LogError(ex, "Error posting adjustments for count {Id}", id);
            return StatusCode(500, "An error occurred while posting adjustments");
        }
    }

    /// <summary>
    /// Cancels a physical count
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromBody] CancelCountDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.CancelAsync(id, dto.Reason, userId);
            if (!result)
                return BadRequest("Failed to cancel physical count");

            return Ok(new { message = "Physical count cancelled successfully" });
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
            _logger.LogError(ex, "Error cancelling physical count {Id}", id);
            return StatusCode(500, "An error occurred while cancelling the physical count");
        }
    }

    /// <summary>
    /// Updates a physical count
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PhysicalCountDto>> Update(Guid id, [FromBody] UpdatePhysicalCountDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _countService.UpdateAsync(id, dto, userId);
            return Ok(count);
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
            _logger.LogError(ex, "Error updating physical count {Id}", id);
            return StatusCode(500, "An error occurred while updating the physical count");
        }
    }

    /// <summary>
    /// Adds an item to the physical count
    /// </summary>
    [HttpPost("{id}/items")]
    public async Task<ActionResult<PhysicalCountItemDto>> AddItem(Guid id, [FromBody] AddCountItemDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var item = await _countService.AddCountItemAsync(id, dto, userId);
            return Ok(item);
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
            _logger.LogError(ex, "Error adding item to physical count {Id}", id);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    /// <summary>
    /// Removes an item from the physical count
    /// </summary>
    [HttpDelete("{id}/items/{itemId}")]
    public async Task<ActionResult> RemoveItem(Guid id, Guid itemId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.RemoveCountItemAsync(itemId, userId);
            if (!result)
                return BadRequest("Failed to remove item");

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
            _logger.LogError(ex, "Error removing item {ItemId} from physical count {Id}", itemId, id);
            return StatusCode(500, "An error occurred while removing the item");
        }
    }

    /// <summary>
    /// Rejects variances for a physical count (sends back for recount)
    /// </summary>
    [HttpPost("{id}/reject-variances")]
    public async Task<ActionResult> RejectVariances(Guid id, [FromBody] RejectDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.RejectVariancesAsync(id, dto.Reason, userId);
            if (!result)
                return BadRequest("Failed to reject variances");

            return Ok(new { message = "Variances rejected, count sent back for review" });
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
            _logger.LogError(ex, "Error rejecting variances for count {Id}", id);
            return StatusCode(500, "An error occurred while rejecting variances");
        }
    }

    /// <summary>
    /// Exports the count sheet for printing or editing
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<ActionResult<PhysicalCountExportDto>> ExportCountSheet(Guid id)
    {
        try
        {
            var export = await _countService.ExportCountSheetAsync(id);
            return Ok(export);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error exporting count sheet {Id}", id);
            return StatusCode(500, "An error occurred while exporting the count sheet");
        }
    }

    /// <summary>
    /// Imports count data from an external source (e.g., Excel)
    /// </summary>
    [HttpPost("{id}/import")]
    public async Task<ActionResult<ImportCountResultDto>> ImportCountSheet(Guid id, [FromBody] List<ImportCountItemDto> items)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _countService.ImportCountSheetAsync(id, items, userId);
            return Ok(result);
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
            _logger.LogError(ex, "Error importing count sheet {Id}", id);
            return StatusCode(500, "An error occurred while importing the count sheet");
        }
    }

    /// <summary>
    /// Gets the variance report for a physical count
    /// </summary>
    [HttpGet("{id}/variance-report")]
    public async Task<ActionResult<VarianceReportDto>> GetVarianceReport(Guid id)
    {
        try
        {
            var report = await _countService.GetVarianceReportAsync(id);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error generating variance report for count {Id}", id);
            return StatusCode(500, "An error occurred while generating the variance report");
        }
    }

    private ActionResult ControlledError(Exception exception, string action, Guid id)
    {
        if (exception is ProcurementAccessAuthorizationException or UnauthorizedAccessException)
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "PHYSICAL_COUNT_FORBIDDEN", message = exception.Message });
        if (exception is ArgumentException)
            return NotFound(new { code = "PHYSICAL_COUNT_NOT_FOUND", message = exception.Message });
        if (exception is InvalidOperationException)
            return Conflict(new { code = "PHYSICAL_COUNT_CONTROL_REJECTED", message = exception.Message });
        _logger.LogError(exception, "Failed to {Action} for physical-count target {Id}", action, id);
        return StatusCode(StatusCodes.Status500InternalServerError,
            new { code = "PHYSICAL_COUNT_FAILED", message = $"Failed to {action}." });
    }
}

/// <summary>
/// DTO for cancelling a physical count
/// </summary>
public class CancelCountDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for rejecting variances
/// </summary>
public class RejectDto
{
    public string Reason { get; set; } = string.Empty;
}

