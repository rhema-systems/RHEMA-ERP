using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class EmergencyProcurementPlansController : ControllerBase
{
    private readonly IEmergencyProcurementPlanService _planService;
    private readonly ILogger<EmergencyProcurementPlansController> _logger;

    public EmergencyProcurementPlansController(
        IEmergencyProcurementPlanService planService,
        ILogger<EmergencyProcurementPlansController> logger)
    {
        _planService = planService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmergencyProcurementPlanDto>>> GetPlans(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] string? emergencyType = null)
    {
        try
        {
            var result = await _planService.GetPlansAsync(page, pageSize, search, status, emergencyType);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emergency procurement plans");
            return StatusCode(500, "An error occurred while retrieving emergency procurement plans");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EmergencyProcurementPlanDetailDto>> GetPlan(Guid id)
    {
        try
        {
            var plan = await _planService.GetByIdAsync(id);
            if (plan == null) return NotFound($"Emergency plan with ID {id} not found");
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emergency plan {PlanId}", id);
            return StatusCode(500, "An error occurred while retrieving the emergency plan");
        }
    }

    [HttpGet("by-number/{planNumber}")]
    public async Task<ActionResult<EmergencyProcurementPlanDto>> GetByPlanNumber(string planNumber)
    {
        try
        {
            var plan = await _planService.GetByPlanNumberAsync(planNumber);
            if (plan == null) return NotFound($"Emergency plan with number {planNumber} not found");
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emergency plan by number {PlanNumber}", planNumber);
            return StatusCode(500, "An error occurred while retrieving the emergency plan");
        }
    }

    [HttpGet("type/{emergencyType}")]
    public async Task<ActionResult<IEnumerable<EmergencyProcurementPlanDto>>> GetByType(string emergencyType)
    {
        try { return Ok(await _planService.GetByEmergencyTypeAsync(emergencyType)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plans for type {EmergencyType}", emergencyType);
            return StatusCode(500, "An error occurred while retrieving plans");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<EmergencyProcurementPlanDto>>> GetActivePlans()
    {
        try { return Ok(await _planService.GetActivePlansAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active emergency plans");
            return StatusCode(500, "An error occurred while retrieving active plans");
        }
    }

    [HttpGet("triggered")]
    public async Task<ActionResult<IEnumerable<EmergencyProcurementPlanDto>>> GetTriggeredPlans()
    {
        try { return Ok(await _planService.GetTriggeredPlansAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting triggered emergency plans");
            return StatusCode(500, "An error occurred while retrieving triggered plans");
        }
    }

    [HttpPost]
    public async Task<ActionResult<EmergencyProcurementPlanDetailDto>> CreatePlan([FromBody] CreateEmergencyProcurementPlanDto dto)
    {
        try
        {
            var plan = await _planService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating emergency plan");
            return StatusCode(500, "An error occurred while creating the emergency plan");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EmergencyProcurementPlanDetailDto>> UpdatePlan(Guid id, [FromBody] CreateEmergencyProcurementPlanDto dto)
    {
        try { return Ok(await _planService.UpdateAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emergency plan {PlanId}", id);
            return StatusCode(500, "An error occurred while updating the emergency plan");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeletePlan(Guid id)
    {
        try { await _planService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting emergency plan {PlanId}", id);
            return StatusCode(500, "An error occurred while deleting the emergency plan");
        }
    }

    [HttpPost("{id}/activate")]
    public async Task<ActionResult<EmergencyProcurementPlanDetailDto>> ActivatePlan(Guid id)
    {
        try { return Ok(await _planService.ActivateAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating emergency plan {PlanId}", id);
            return StatusCode(500, "An error occurred while activating the emergency plan");
        }
    }

    [HttpPost("{id}/trigger")]
    public async Task<ActionResult<EmergencyProcurementPlanDetailDto>> TriggerPlan(Guid id)
    {
        try { return Ok(await _planService.TriggerAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering emergency plan {PlanId}", id);
            return StatusCode(500, "An error occurred while triggering the emergency plan");
        }
    }

    [HttpPost("{id}/deactivate")]
    public async Task<ActionResult<EmergencyProcurementPlanDetailDto>> DeactivatePlan(Guid id)
    {
        try { return Ok(await _planService.DeactivateAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating emergency plan {PlanId}", id);
            return StatusCode(500, "An error occurred while deactivating the emergency plan");
        }
    }

    [HttpPost("{planId}/items")]
    public async Task<ActionResult<EmergencyProcurementItemDto>> AddItem(Guid planId, [FromBody] CreateEmergencyProcurementItemDto dto)
    {
        try { return Ok(await _planService.AddItemAsync(planId, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to emergency plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    [HttpPut("items/{itemId}")]
    public async Task<ActionResult<EmergencyProcurementItemDto>> UpdateItem(Guid itemId, [FromBody] CreateEmergencyProcurementItemDto dto)
    {
        try { return Ok(await _planService.UpdateItemAsync(itemId, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emergency item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while updating the item");
        }
    }

    [HttpDelete("items/{itemId}")]
    public async Task<ActionResult> DeleteItem(Guid itemId)
    {
        try { await _planService.DeleteItemAsync(itemId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting emergency item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while deleting the item");
        }
    }

    [HttpGet("{planId}/items/critical")]
    public async Task<ActionResult<IEnumerable<EmergencyProcurementItemDto>>> GetCriticalItems(Guid planId)
    {
        try { return Ok(await _planService.GetCriticalItemsAsync(planId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting critical items for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving critical items");
        }
    }

    [HttpPost("{planId}/suppliers")]
    public async Task<ActionResult<EmergencySupplierDto>> AddSupplier(Guid planId, [FromBody] CreateEmergencySupplierDto dto)
    {
        try { return Ok(await _planService.AddSupplierAsync(planId, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding supplier to emergency plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while adding the supplier");
        }
    }

    [HttpPut("suppliers/{supplierId}")]
    public async Task<ActionResult<EmergencySupplierDto>> UpdateSupplier(Guid supplierId, [FromBody] CreateEmergencySupplierDto dto)
    {
        try { return Ok(await _planService.UpdateSupplierAsync(supplierId, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emergency supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while updating the supplier");
        }
    }

    [HttpDelete("suppliers/{supplierId}")]
    public async Task<ActionResult> DeleteSupplier(Guid supplierId)
    {
        try { await _planService.DeleteSupplierAsync(supplierId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting emergency supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while deleting the supplier");
        }
    }

    [HttpGet("{planId}/suppliers/active")]
    public async Task<ActionResult<IEnumerable<EmergencySupplierDto>>> GetActiveSuppliers(Guid planId)
    {
        try { return Ok(await _planService.GetActiveSuppliersAsync(planId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active suppliers for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving active suppliers");
        }
    }

    [HttpGet("suppliers/category/{category}")]
    public async Task<ActionResult<IEnumerable<EmergencySupplierDto>>> GetSuppliersByCategory(string category)
    {
        try { return Ok(await _planService.GetSuppliersByCategoryAsync(category)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suppliers for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving suppliers");
        }
    }
}