using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class SupplierConsolidationsController : ControllerBase
{
    private readonly ISupplierConsolidationService _consolidationService;
    private readonly ILogger<SupplierConsolidationsController> _logger;

    public SupplierConsolidationsController(
        ISupplierConsolidationService consolidationService,
        ILogger<SupplierConsolidationsController> logger)
    {
        _consolidationService = consolidationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierConsolidationDto>>> GetConsolidations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null)
    {
        try
        {
            var result = await _consolidationService.GetConsolidationsAsync(page, pageSize, search, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier consolidations");
            return StatusCode(500, "An error occurred while retrieving supplier consolidations");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierConsolidationDetailDto>> GetConsolidation(Guid id)
    {
        try
        {
            var consolidation = await _consolidationService.GetByIdAsync(id);
            if (consolidation == null) return NotFound($"Supplier consolidation with ID {id} not found");
            return Ok(consolidation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier consolidation {ConsolidationId}", id);
            return StatusCode(500, "An error occurred while retrieving the supplier consolidation");
        }
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SupplierConsolidationDto>>> GetByCategory(string category)
    {
        try { return Ok(await _consolidationService.GetByCategoryAsync(category)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consolidations for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving consolidations");
        }
    }

    [HttpGet("supplier/{supplierId}")]
    public async Task<ActionResult<IEnumerable<SupplierConsolidationDto>>> GetBySupplier(Guid supplierId)
    {
        try { return Ok(await _consolidationService.GetBySupplierAsync(supplierId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consolidations for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while retrieving consolidations");
        }
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<SupplierConsolidationDto>>> GetPending()
    {
        try { return Ok(await _consolidationService.GetPendingConsolidationsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending consolidations");
            return StatusCode(500, "An error occurred while retrieving pending consolidations");
        }
    }

    [HttpGet("implemented")]
    public async Task<ActionResult<IEnumerable<SupplierConsolidationDto>>> GetImplemented()
    {
        try { return Ok(await _consolidationService.GetImplementedConsolidationsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting implemented consolidations");
            return StatusCode(500, "An error occurred while retrieving implemented consolidations");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SupplierConsolidationDetailDto>> CreateConsolidation([FromBody] CreateSupplierConsolidationDto dto)
    {
        try
        {
            var consolidation = await _consolidationService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetConsolidation), new { id = consolidation.Id }, consolidation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier consolidation");
            return StatusCode(500, "An error occurred while creating the supplier consolidation");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SupplierConsolidationDetailDto>> UpdateConsolidation(Guid id, [FromBody] CreateSupplierConsolidationDto dto)
    {
        try { return Ok(await _consolidationService.UpdateAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier consolidation {ConsolidationId}", id);
            return StatusCode(500, "An error occurred while updating the supplier consolidation");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteConsolidation(Guid id)
    {
        try { await _consolidationService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supplier consolidation {ConsolidationId}", id);
            return StatusCode(500, "An error occurred while deleting the supplier consolidation");
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<SupplierConsolidationDetailDto>> ApproveConsolidation(Guid id)
    {
        try { return Ok(await _consolidationService.ApproveAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving supplier consolidation {ConsolidationId}", id);
            return StatusCode(500, "An error occurred while approving the supplier consolidation");
        }
    }

    [HttpPost("{id}/implement")]
    public async Task<ActionResult<SupplierConsolidationDetailDto>> ImplementConsolidation(Guid id)
    {
        try { return Ok(await _consolidationService.ImplementAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error implementing supplier consolidation {ConsolidationId}", id);
            return StatusCode(500, "An error occurred while implementing the supplier consolidation");
        }
    }

    [HttpPost("{id}/record-savings")]
    public async Task<ActionResult> RecordActualSavings(Guid id, [FromQuery] decimal actualSavings)
    {
        try { await _consolidationService.RecordActualSavingsAsync(id, actualSavings); return Ok(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording savings for consolidation {ConsolidationId}", id);
            return StatusCode(500, "An error occurred while recording savings");
        }
    }

    [HttpGet("total-estimated-savings")]
    public async Task<ActionResult<decimal>> GetTotalEstimatedSavings()
    {
        try { return Ok(await _consolidationService.GetTotalEstimatedSavingsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total estimated savings");
            return StatusCode(500, "An error occurred while retrieving total estimated savings");
        }
    }

    [HttpGet("total-actual-savings")]
    public async Task<ActionResult<decimal>> GetTotalActualSavings()
    {
        try { return Ok(await _consolidationService.GetTotalActualSavingsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total actual savings");
            return StatusCode(500, "An error occurred while retrieving total actual savings");
        }
    }
}