using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing organization structures
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class OrganizationStructureController : ControllerBase
{
    private readonly IOrganizationStructureService _organizationStructureService;
    private readonly ILogger<OrganizationStructureController> _logger;

    public OrganizationStructureController(
        IOrganizationStructureService organizationStructureService,
        ILogger<OrganizationStructureController> logger)
    {
        _organizationStructureService = organizationStructureService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all organization structures
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _organizationStructureService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all organization structures");
            return StatusCode(500, "An error occurred while retrieving organization structures");
        }
    }

    /// <summary>
    /// Retrieves organization structures with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<OrganizationStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _organizationStructureService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged organization structures");
            return StatusCode(500, "An error occurred while retrieving organization structures");
        }
    }

    /// <summary>
    /// Retrieves all organization structures as summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationStructureSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSummary()
    {
        try
        {
            var response = await _organizationStructureService.GetAllSummaryAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization structure summaries");
            return StatusCode(500, "An error occurred while retrieving organization structure summaries");
        }
    }

    /// <summary>
    /// Retrieves an organization structure by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _organizationStructureService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization structure with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization structure");
        }
    }

    /// <summary>
    /// Retrieves detailed organization structure by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(OrganizationStructureDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _organizationStructureService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization structure detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the organization structure detail");
        }
    }

    /// <summary>
    /// Retrieves the default organization structure
    /// </summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(OrganizationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefaultStructure()
    {
        try
        {
            var response = await _organizationStructureService.GetDefaultStructureAsync();
            if (response == null)
                return NotFound(new { message = "No default organization structure found" });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving default organization structure");
            return StatusCode(500, "An error occurred while retrieving the default organization structure");
        }
    }

    /// <summary>
    /// Creates a new organization structure
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationStructureDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationStructureService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization structure");
            return StatusCode(500, "An error occurred while creating the organization structure");
        }
    }

    /// <summary>
    /// Updates an existing organization structure
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationStructureDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _organizationStructureService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization structure with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the organization structure");
        }
    }

    /// <summary>
    /// Deletes an organization structure
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _organizationStructureService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization structure with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the organization structure");
        }
    }

    /// <summary>
    /// Sets an organization structure as default
    /// </summary>
    [HttpPatch("{id:guid}/set-default")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAsDefault(Guid id)
    {
        try
        {
            var response = await _organizationStructureService.SetAsDefaultAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting organization structure as default with ID {Id}", id);
            return StatusCode(500, "An error occurred while setting the organization structure as default");
        }
    }
}
