using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing location structures
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class LocationStructureController : ControllerBase
{
    private readonly ILocationStructureService _locationStructureService;
    private readonly ILogger<LocationStructureController> _logger;

    public LocationStructureController(
        ILocationStructureService locationStructureService,
        ILogger<LocationStructureController> logger)
    {
        _locationStructureService = locationStructureService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all location structures
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LocationStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _locationStructureService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all location structures");
            return StatusCode(500, "An error occurred while retrieving location structures");
        }
    }

    /// <summary>
    /// Retrieves location structures with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<LocationStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _locationStructureService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged location structures");
            return StatusCode(500, "An error occurred while retrieving location structures");
        }
    }

    /// <summary>
    /// Retrieves all location structures as summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<LocationStructureSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSummary()
    {
        try
        {
            var response = await _locationStructureService.GetAllSummaryAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location structure summaries");
            return StatusCode(500, "An error occurred while retrieving location structure summaries");
        }
    }

    /// <summary>
    /// Retrieves a location structure by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LocationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _locationStructureService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location structure with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location structure");
        }
    }

    /// <summary>
    /// Retrieves detailed location structure by ID
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(LocationStructureDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailById(Guid id)
    {
        try
        {
            var response = await _locationStructureService.GetDetailByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location structure detail with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the location structure detail");
        }
    }

    /// <summary>
    /// Retrieves the default location structure
    /// </summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(LocationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefaultStructure()
    {
        try
        {
            var response = await _locationStructureService.GetDefaultStructureAsync();
            if (response == null)
                return NotFound(new { message = "No default location structure found" });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving default location structure");
            return StatusCode(500, "An error occurred while retrieving the default location structure");
        }
    }

    /// <summary>
    /// Creates a new location structure
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(LocationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLocationStructureDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationStructureService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location structure");
            return StatusCode(500, "An error occurred while creating the location structure");
        }
    }

    /// <summary>
    /// Updates an existing location structure
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(LocationStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationStructureDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _locationStructureService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location structure with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the location structure");
        }
    }

    /// <summary>
    /// Deletes a location structure
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _locationStructureService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location structure with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the location structure");
        }
    }

    /// <summary>
    /// Sets a location structure as default
    /// </summary>
    [HttpPatch("{id:guid}/set-default")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAsDefault(Guid id)
    {
        try
        {
            var response = await _locationStructureService.SetAsDefaultAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting location structure as default with ID {Id}", id);
            return StatusCode(500, "An error occurred while setting the location structure as default");
        }
    }
}
