using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Pricing;

[Authorize]
[ApiController]
[Route("api/pricing/customer-groups")]
public class CustomerGroupsController : ControllerBase
{
    private readonly ICustomerGroupService _customerGroupService;
    private readonly ILogger<CustomerGroupsController> _logger;

    public CustomerGroupsController(
        ICustomerGroupService customerGroupService,
        ILogger<CustomerGroupsController> logger)
    {
        _customerGroupService = customerGroupService;
        _logger = logger;
    }

    /// <summary>
    /// Get all customer groups
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<CustomerGroupDto>>> GetAll()
    {
        try
        {
            var groups = await _customerGroupService.GetAllAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer groups");
            return StatusCode(500, "An error occurred while retrieving customer groups");
        }
    }

    /// <summary>
    /// Get active customer groups
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<CustomerGroupDto>>> GetActive()
    {
        try
        {
            var groups = await _customerGroupService.GetActiveAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active customer groups");
            return StatusCode(500, "An error occurred while retrieving customer groups");
        }
    }

    /// <summary>
    /// Get customer group by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<CustomerGroupDto>> GetById(Guid id)
    {
        try
        {
            var group = await _customerGroupService.GetByIdAsync(id);
            if (group == null)
                return NotFound($"Customer group with ID {id} not found");
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer group {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the customer group");
        }
    }

    /// <summary>
    /// Get customer group by code
    /// </summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<CustomerGroupDto>> GetByCode(string code)
    {
        try
        {
            var group = await _customerGroupService.GetByCodeAsync(code);
            if (group == null)
                return NotFound($"Customer group with code '{code}' not found");
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer group by code {Code}", code);
            return StatusCode(500, "An error occurred while retrieving the customer group");
        }
    }

    /// <summary>
    /// Create a new customer group
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<CustomerGroupDto>> Create([FromBody] CreateCustomerGroupDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var group = await _customerGroupService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = group.Id }, group);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer group");
            return StatusCode(500, "An error occurred while creating the customer group");
        }
    }

    /// <summary>
    /// Update a customer group
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<CustomerGroupDto>> Update(Guid id, [FromBody] UpdateCustomerGroupDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var group = await _customerGroupService.UpdateAsync(id, dto);
            return Ok(group);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer group {Id}", id);
            return StatusCode(500, "An error occurred while updating the customer group");
        }
    }

    /// <summary>
    /// Delete a customer group
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _customerGroupService.DeleteAsync(id);
            if (!result)
                return NotFound($"Customer group with ID {id} not found");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer group {Id}", id);
            return StatusCode(500, "An error occurred while deleting the customer group");
        }
    }
}

