using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Pricing;

[Authorize]
[ApiController]
[Route("api/pricing/supplier-groups")]
public class SupplierGroupsController : ControllerBase
{
    private readonly ISupplierGroupService _supplierGroupService;
    private readonly ILogger<SupplierGroupsController> _logger;

    public SupplierGroupsController(
        ISupplierGroupService supplierGroupService,
        ILogger<SupplierGroupsController> logger)
    {
        _supplierGroupService = supplierGroupService;
        _logger = logger;
    }

    /// <summary>
    /// Get all supplier groups
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<SupplierGroupDto>>> GetAll()
    {
        try
        {
            var groups = await _supplierGroupService.GetAllAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier groups");
            return StatusCode(500, "An error occurred while retrieving supplier groups");
        }
    }

    /// <summary>
    /// Get active supplier groups
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<SupplierGroupDto>>> GetActive()
    {
        try
        {
            var groups = await _supplierGroupService.GetActiveAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active supplier groups");
            return StatusCode(500, "An error occurred while retrieving supplier groups");
        }
    }

    /// <summary>
    /// Get supplier group by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<SupplierGroupDto>> GetById(Guid id)
    {
        try
        {
            var group = await _supplierGroupService.GetByIdAsync(id);
            if (group == null)
                return NotFound($"Supplier group with ID {id} not found");
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier group {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the supplier group");
        }
    }

    /// <summary>
    /// Get supplier group by code
    /// </summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<SupplierGroupDto>> GetByCode(string code)
    {
        try
        {
            var group = await _supplierGroupService.GetByCodeAsync(code);
            if (group == null)
                return NotFound($"Supplier group with code '{code}' not found");
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier group by code {Code}", code);
            return StatusCode(500, "An error occurred while retrieving the supplier group");
        }
    }

    /// <summary>
    /// Create a new supplier group
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<SupplierGroupDto>> Create([FromBody] CreateSupplierGroupDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var group = await _supplierGroupService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = group.Id }, group);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier group");
            return StatusCode(500, "An error occurred while creating the supplier group");
        }
    }

    /// <summary>
    /// Update a supplier group
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<SupplierGroupDto>> Update(Guid id, [FromBody] UpdateSupplierGroupDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var group = await _supplierGroupService.UpdateAsync(id, dto);
            return Ok(group);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier group {Id}", id);
            return StatusCode(500, "An error occurred while updating the supplier group");
        }
    }

    /// <summary>
    /// Delete a supplier group
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _supplierGroupService.DeleteAsync(id);
            if (!result)
                return NotFound($"Supplier group with ID {id} not found");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supplier group {Id}", id);
            return StatusCode(500, "An error occurred while deleting the supplier group");
        }
    }
}

