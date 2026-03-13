using System.Security.Claims;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/landed-costs")]
[Authorize]
public class LandedCostsController : ControllerBase
{
    private readonly ILandedCostService _landedCostService;
    private readonly ILogger<LandedCostsController> _logger;

    public LandedCostsController(
        ILandedCostService landedCostService,
        ILogger<LandedCostsController> logger)
    {
        _landedCostService = landedCostService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LandedCostDto>>> GetAll()
    {
        try
        {
            var landedCosts = await _landedCostService.GetAllAsync();
            return Ok(landedCosts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landed costs");
            return StatusCode(500, "An error occurred while retrieving landed costs");
        }
    }

    [HttpGet("by-grn/{grnId}")]
    public async Task<ActionResult<IEnumerable<LandedCostDto>>> GetByGrn(Guid grnId, [FromQuery] bool ensure = false)
    {
        try
        {
            _logger.LogInformation("Get landed costs by GRN {GrnId} (ensure={Ensure})", grnId, ensure);
            var landedCosts = (await _landedCostService.GetByGRNAsync(grnId)).ToList();
            // NOTE:
            // We intentionally do NOT auto-create landed cost vouchers on GET (even if ensure=true).
            // In ERP workflows, document creation should be an explicit user action or a defined business event
            // (e.g., GRN posting), not a side-effect of opening a screen.

            _logger.LogInformation("Returned {Count} landed cost voucher(s) for GRN {GrnId}", landedCosts.Count, grnId);
            return Ok(landedCosts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landed costs for GRN {GrnId}", grnId);
            return StatusCode(500, "An error occurred while retrieving landed costs");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LandedCostDetailDto>> GetById(Guid id)
    {
        try
        {
            var landedCost = await _landedCostService.GetByIdAsync(id);
            if (landedCost == null)
                return NotFound($"Landed cost with ID {id} not found");

            return Ok(landedCost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landed cost {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the landed cost");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LandedCostDto>> Create([FromBody] CreateLandedCostDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var landedCost = await _landedCostService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = landedCost.Id }, landedCost);
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
            _logger.LogError(ex, "Error creating landed cost");
            return StatusCode(500, "An error occurred while creating the landed cost");
        }
    }

    [HttpPost("initialize-from-po/{grnId}")]
    public async Task<ActionResult<LandedCostDto>> InitializeFromPo(Guid grnId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Initialize landed cost from PO plan for GRN {GrnId} by {UserId}", grnId, userId);
            var landedCost = await _landedCostService.InitializeFromPurchaseOrderPlanAsync(grnId, userId);
            _logger.LogInformation("Initialized landed cost {LandedCostId} ({LandedCostNumber}) for GRN {GrnId}", landedCost.Id, landedCost.LandedCostNumber, grnId);
            return Ok(landedCost);
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
            _logger.LogError(ex, "Error initializing landed cost from PO plan for GRN {GrnId}", grnId);
            return StatusCode(500, "An error occurred while initializing landed cost");
        }
    }

    [HttpPost("{id}/allocate")]
    public async Task<ActionResult> Allocate(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.AllocateCostsAsync(id, userId);
            if (!ok) return BadRequest("Allocation failed");
            return Ok(new { message = "Landed costs allocated successfully" });
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
            _logger.LogError(ex, "Error allocating landed cost {Id}", id);
            return StatusCode(500, "An error occurred while allocating landed cost");
        }
    }

    [HttpPut("{landedCostId}/items/{landedCostItemId}/manual-allocations")]
    public async Task<ActionResult> SetManualAllocations(Guid landedCostId, Guid landedCostItemId, [FromBody] SetManualLandedCostAllocationsDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.SetManualAllocationsAsync(landedCostId, landedCostItemId, dto, userId);
            if (!ok) return BadRequest("Failed to set manual allocations");
            return Ok(new { message = "Manual allocations saved successfully" });
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
            _logger.LogError(ex, "Error setting manual allocations for landed cost {LandedCostId} item {ItemId}", landedCostId, landedCostItemId);
            return StatusCode(500, "An error occurred while saving manual allocations");
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.ApproveAsync(id, userId);
            if (!ok) return BadRequest("Approval failed");
            return Ok(new { message = "Landed cost approved successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving landed cost {Id}", id);
            return StatusCode(500, "An error occurred while approving landed cost");
        }
    }

    [HttpPost("{id}/post-to-inventory")]
    public async Task<ActionResult> PostToInventory(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.PostToInventoryAsync(id, userId);
            if (!ok) return BadRequest("Posting failed");
            return Ok(new { message = "Landed cost posted to inventory successfully" });
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
            _logger.LogError(ex, "Error posting landed cost {Id} to inventory", id);
            return StatusCode(500, "An error occurred while posting landed cost to inventory");
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromQuery] string reason)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ok = await _landedCostService.CancelAsync(id, reason, userId);
            if (!ok) return BadRequest("Cancel failed");
            return Ok(new { message = "Landed cost cancelled successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling landed cost {Id}", id);
            return StatusCode(500, "An error occurred while cancelling landed cost");
        }
    }
}
