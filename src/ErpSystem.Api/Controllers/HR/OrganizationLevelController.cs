using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing organization levels
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class OrganizationLevelController : ControllerBase
{
    private readonly IOrganizationLevelService _organizationLevelService;
    private readonly ILogger<OrganizationLevelController> _logger;

    public OrganizationLevelController(
        IOrganizationLevelService organizationLevelService,
        ILogger<OrganizationLevelController> logger)
    {
        _organizationLevelService = organizationLevelService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all organization levels
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _organizationLevelService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all organization levels");
            return StatusCode(500, "An error occurred while retrieving organization levels");
        }
    }

    /// <summary>
    /// Retrieves organization levels with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<OrganizationLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _organizationLevelService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged organization levels");
            return StatusCode(500, "An error occurred while retrieving organization levels");
        }
    }

    /// <summary>
    /// Retrieves all organization levels as summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationLevelSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSummary()
    {
        try
        {
            var response = await _organizationLevelService.GetAllSummaryAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization level summaries");
            return StatusCode(500, "An error occurred while retrieving organization level summaries");
        }
    }

    /// <summary>
    /// Retrieves an organization level by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _organizationLevelService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization level with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization level");
        }
    }

    /// <summary>
    /// Retrieves detailed organization level by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(OrganizationLevelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _organizationLevelService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization level detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization level detail");
        }
    }

    /// <summary>
    /// Retrieves organization levels by structure ID
    /// </summary>
    [HttpGet("structure/{structureId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStructureId(Guid structureId)
    {
        try
        {
            var response = await _organizationLevelService.GetByStructureIdAsync(structureId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization levels for structure {StructureId}", structureId);
            return StatusCode(500, "An error occurred while retrieving organization levels");
        }
    }

    /// <summary>
    /// Creates a new organization level
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationLevelDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationLevelService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization level");
            return StatusCode(500, "An error occurred while creating the organization level");
        }
    }

    /// <summary>
    /// Updates an existing organization level
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationLevelDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationLevelService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization level with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the organization level");
        }
    }

    /// <summary>
    /// Deletes an organization level
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _organizationLevelService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization level with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the organization level");
        }
    }
}
